using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class MySqlScriptGenerator : DbScriptGenerator
    {
        public MySqlScriptGenerator(DbInterpreter dbInterpreter) : base(dbInterpreter)
        {
        }

        #region Data Script

        protected override string GetBytesConvertHexString(object value, string dataType)
        {
            var hex = string.Concat(((byte[])value).Select(item => item.ToString("X2")));
            return $"UNHEX('{hex}')";
        }

        #endregion

        #region Generate Schema Scripts

        public override ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo)
        {
            var sb = new ScriptBuilder();

            #region Function

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Functions));

            #endregion

            #region Table

            foreach (var table in schemaInfo.Tables)
            {
                FeedbackInfo(OperationState.Begin, table);

                IEnumerable<TableColumn> columns = schemaInfo.TableColumns.Where(item => item.TableName == table.Name)
                    .OrderBy(item => item.Order);

                var primaryKey = schemaInfo.TablePrimaryKeys.FirstOrDefault(item => item.TableName == table.Name);
                var foreignKeys = schemaInfo.TableForeignKeys.Where(item => item.TableName == table.Name);
                IEnumerable<TableIndex> indexes = schemaInfo.TableIndexes.Where(item => item.TableName == table.Name)
                    .OrderBy(item => item.Order);
                IEnumerable<TableConstraint> constraints = schemaInfo.TableConstraints
                    .Where(item => item.TableName == table.Name).OrderBy(item => item.Order);

                var sbTable = CreateTable(table, columns, primaryKey, foreignKeys, indexes, constraints);

                sb.AppendRange(sbTable.Scripts);

                FeedbackInfo(OperationState.End, table);
            }

            #endregion

            #region View

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Views));

            #endregion

            #region Trigger

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.TableTriggers));

            #endregion

            #region Procedure

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Procedures));

            #endregion

            if (option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                AppendScriptsToFile(sb.ToString(), GenerateScriptMode.Schema, true);

            return sb;
        }

        private void RestrictColumnLength<T>(IEnumerable<TableColumn> columns, IEnumerable<T> children)
            where T : SimpleColumn
        {
            if (children == null) return;

            var childColumns = columns.Where(item => children.Any(t => item.Name == t.ColumnName)).ToList();

            childColumns.ForEach(item =>
            {
                if (DataTypeHelper.IsCharType(item.DataType) &&
                    item.MaxLength > MySqlInterpreter.KeyIndexColumnMaxLength)
                    item.MaxLength = MySqlInterpreter.KeyIndexColumnMaxLength;
            });
        }

        private string GetRestrictedLengthName(string name)
        {
            if (name != null && name.Length > MySqlInterpreter.NameMaxLength)
                return name.Substring(0, MySqlInterpreter.NameMaxLength);

            return name;
        }

        #endregion

        #region Alter Table

        public override Script RenameTable(Table table, string newName)
        {
            return new AlterDbObjectScript<Table>(
                $"ALTER TABLE {GetQuotedString(table.Name)} RENAME {GetQuotedString(newName)};");
        }

        public override Script SetTableComment(Table table, bool isNew = true)
        {
            return new AlterDbObjectScript<Table>(
                $"ALTER TABLE {GetQuotedString(table.Name)} COMMENT = '{dbInterpreter.ReplaceSplitChar(ValueHelper.TransferSingleQuotation(table.Comment))}';");
        }

        public override Script AddTableColumn(Table table, TableColumn column)
        {
            return new CreateDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} ADD {dbInterpreter.ParseColumn(table, column)};");
        }

        public override Script RenameTableColumn(Table table, TableColumn column, string newName)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} CHANGE {GetQuotedString(column.Name)} {newName} {dbInterpreter.ParseDataType(column)};");
        }

        public override Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} MODIFY COLUMN {dbInterpreter.ParseColumn(table, newColumn)};");
        }

        public override Script SetTableColumnComment(Table table, TableColumn column, bool isNew = true)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(column.TableName)} MODIFY COLUMN {dbInterpreter.ParseColumn(table, column)};");
        }

        public override Script DropTableColumn(TableColumn column)
        {
            return new DropDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(column.TableName)} DROP COLUMN {GetQuotedString(column.Name)};");
        }

        public override Script AddPrimaryKey(TablePrimaryKey primaryKey)
        {
            var columnNames = string.Join(",", primaryKey.Columns.Select(item => GetQuotedString(item.ColumnName)));

            var sql =
                $"ALTER TABLE {GetQuotedString(primaryKey.TableName)} ADD CONSTRAINT {GetQuotedString(GetRestrictedLengthName(primaryKey.Name))} PRIMARY KEY ({columnNames})";

            if (option.TableScriptsGenerateOption.GenerateComment)
                if (!string.IsNullOrEmpty(primaryKey.Comment))
                    sql += $" COMMENT '{TransferSingleQuotationString(primaryKey.Comment)}'";

            return new CreateDbObjectScript<TablePrimaryKey>(sql + scriptsDelimiter);
        }

        public override Script DropPrimaryKey(TablePrimaryKey primaryKey)
        {
            return new DropDbObjectScript<TablePrimaryKey>(
                $"ALTER TABLE {GetQuotedString(primaryKey.TableName)} DROP PRIMARY KEY;");
        }

        public override Script AddForeignKey(TableForeignKey foreignKey)
        {
            var columnNames = string.Join(",", foreignKey.Columns.Select(item => GetQuotedString(item.ColumnName)));
            var referenceColumnName = string.Join(",",
                foreignKey.Columns.Select(item => $"{GetQuotedString(item.ReferencedColumnName)}"));

            var sql =
                $"ALTER TABLE {GetQuotedString(foreignKey.TableName)} ADD CONSTRAINT {GetQuotedString(GetRestrictedLengthName(foreignKey.Name))} FOREIGN KEY ({columnNames}) REFERENCES {GetQuotedString(foreignKey.ReferencedTableName)}({referenceColumnName})";

            if (foreignKey.UpdateCascade)
                sql += " ON UPDATE CASCADE";
            else
                sql += " ON UPDATE NO ACTION";

            if (foreignKey.DeleteCascade)
                sql += " ON DELETE CASCADE";
            else
                sql += " ON DELETE NO ACTION";

            return new CreateDbObjectScript<TableForeignKey>(sql + scriptsDelimiter);
        }

        public override Script DropForeignKey(TableForeignKey foreignKey)
        {
            return new DropDbObjectScript<TableForeignKey>(
                $"ALTER TABLE {GetQuotedString(foreignKey.TableName)} DROP FOREIGN KEY {GetQuotedString(foreignKey.Name)}");
        }

        public override Script AddIndex(TableIndex index)
        {
            var columnNames = string.Join(",", index.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));

            var type = "";

            if (index.Type == IndexType.Unique.ToString())
                type = "UNIQUE";
            else if (index.Type == IndexType.FullText.ToString()) type = "FULLTEXT";

            var sql =
                $"ALTER TABLE {GetQuotedString(index.TableName)} ADD {type} INDEX {GetQuotedString(GetRestrictedLengthName(index.Name))} ({columnNames})";

            if (option.TableScriptsGenerateOption.GenerateComment)
                if (!string.IsNullOrEmpty(index.Comment))
                    sql += $" COMMENT '{TransferSingleQuotationString(index.Comment)}'";

            return new CreateDbObjectScript<TableIndex>(sql + scriptsDelimiter);
        }

        public override Script DropIndex(TableIndex index)
        {
            return new DropDbObjectScript<TableIndex>(
                $"ALTER TABLE {GetQuotedString(index.TableName)} DROP INDEX {GetQuotedString(index.Name)};");
        }

        public override Script AddCheckConstraint(TableConstraint constraint)
        {
            return new CreateDbObjectScript<TableConstraint>(
                $"ALTER TABLE {GetQuotedFullTableName(constraint)} ADD CONSTRAINT {GetQuotedString(constraint.Name)} CHECK  ({constraint.Definition})");
        }

        public override Script DropCheckConstraint(TableConstraint constraint)
        {
            return new Script(
                $"ALTER TABLE {GetQuotedFullTableName(constraint)} DROP CONSTRAINT {GetQuotedString(constraint.Name)}");
        }

        public override Script SetIdentityEnabled(TableColumn column, bool enabled)
        {
            var table = new Table { Schema = column.Schema, Name = column.TableName };

            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(column.TableName)} MODIFY COLUMN {dbInterpreter.ParseColumn(table, column)} {(enabled ? "AUTO_INCREMENT" : "")}");
        }

        #endregion

        #region Database Operation

        public override Script CreateSchema(DatabaseSchema schema)
        {
            return new Script("");
        }

        public override Script CreateUserDefinedType(UserDefinedType userDefinedType)
        {
            return new Script("");
        }

        public override Script CreateSequence(Sequence sequence)
        {
            return new Script("");
        }

        public override ScriptBuilder CreateTable(Table table, IEnumerable<TableColumn> columns,
            TablePrimaryKey primaryKey,
            IEnumerable<TableForeignKey> foreignKeys,
            IEnumerable<TableIndex> indexes,
            IEnumerable<TableConstraint> constraints)
        {
            var sb = new ScriptBuilder();

            var mySqlInterpreter = dbInterpreter as MySqlInterpreter;
            var dbCharSet = mySqlInterpreter.DbCharset;
            var notCreateIfExistsClause = mySqlInterpreter.NotCreateIfExistsClause;

            var tableName = table.Name;
            var quotedTableName = GetQuotedFullTableName(table);

            RestrictColumnLength(columns, primaryKey?.Columns);
            RestrictColumnLength(columns, foreignKeys.SelectMany(item => item.Columns));
            RestrictColumnLength(columns, indexes.SelectMany(item => item.Columns));

            var primaryKeyColumns = "";

            if (this.option.TableScriptsGenerateOption.GeneratePrimaryKey && primaryKey != null)
            {
                var pkColumns = new List<IndexColumn>();

                //identity column must be the first primary key column.
                pkColumns.AddRange(primaryKey.Columns.Where(item =>
                    columns.Any(col => col.Name == item.ColumnName && col.IsIdentity)));
                pkColumns.AddRange(primaryKey.Columns.Where(item =>
                    columns.Any(col => col.Name == item.ColumnName && !col.IsIdentity)));

                var primaryKeyName = GetQuotedString(primaryKey.Name);

                primaryKeyColumns =
                    $@"
,PRIMARY KEY
(
  {string.Join(Environment.NewLine, pkColumns.Select(item => $"{GetQuotedString(item.ColumnName)},")).TrimEnd(',')}
)";
            }

            #region Table

            var option = GetCreateTableOption();

            var tableScript =
                $@"
CREATE TABLE {notCreateIfExistsClause} {quotedTableName}(
{string.Join("," + Environment.NewLine, columns.Select(item => dbInterpreter.ParseColumn(table, item)))}{primaryKeyColumns}
){(!string.IsNullOrEmpty(table.Comment) && this.option.TableScriptsGenerateOption.GenerateComment ? $"comment='{dbInterpreter.ReplaceSplitChar(ValueHelper.TransferSingleQuotation(table.Comment))}'" : "")}
DEFAULT CHARSET={dbCharSet}" + (string.IsNullOrEmpty(option) ? "" : Environment.NewLine + option) + scriptsDelimiter;

            sb.AppendLine(new CreateDbObjectScript<Table>(tableScript));

            #endregion

            //#region Primary Key
            //if (this.option.TableScriptsGenerateOption.GeneratePrimaryKey && primaryKeys.Count() > 0)
            //{
            //    TablePrimaryKey primaryKey = primaryKeys.FirstOrDefault();

            //    if (primaryKey != null)
            //    {
            //        sb.AppendLine(this.AddPrimaryKey(primaryKey));
            //    }
            //}
            //#endregion

            var foreignKeysLines = new List<string>();

            #region Foreign Key

            if (this.option.TableScriptsGenerateOption.GenerateForeignKey)
                foreach (var foreignKey in foreignKeys)
                    sb.AppendLine(AddForeignKey(foreignKey));

            #endregion

            #region Index

            if (this.option.TableScriptsGenerateOption.GenerateIndex)
                foreach (var index in indexes)
                    sb.AppendLine(AddIndex(index));

            #endregion

            #region Constraint

            if (this.option.TableScriptsGenerateOption.GenerateConstraint && constraints != null)
                foreach (var constraint in constraints)
                    sb.AppendLine(AddCheckConstraint(constraint));

            #endregion

            sb.AppendLine();

            return sb;
        }

        public override Script DropUserDefinedType(UserDefinedType userDefinedType)
        {
            return new Script("");
        }

        public override Script DropSequence(Sequence sequence)
        {
            return new Script("");
        }

        public override Script DropTable(Table table)
        {
            return new DropDbObjectScript<Table>(GetDropSql(nameof(DatabaseObjectType.Table), table));
        }

        public override Script DropView(View view)
        {
            return new DropDbObjectScript<View>(GetDropSql(nameof(DatabaseObjectType.View), view));
        }

        public override Script DropTrigger(TableTrigger trigger)
        {
            return new DropDbObjectScript<View>(GetDropSql(nameof(DatabaseObjectType.Trigger), trigger));
        }

        public override Script DropFunction(Function function)
        {
            return new DropDbObjectScript<Function>(GetDropSql(nameof(DatabaseObjectType.Function), function));
        }

        public override Script DropProcedure(Procedure procedure)
        {
            return new DropDbObjectScript<Procedure>(GetDropSql(nameof(DatabaseObjectType.Procedure), procedure));
        }

        private string GetDropSql(string typeName, DatabaseObject dbObject)
        {
            return $"DROP {typeName.ToUpper()} IF EXISTS {GetQuotedDbObjectNameWithSchema(dbObject)};";
        }

        public override IEnumerable<Script> SetConstrainsEnabled(bool enabled)
        {
            yield return new ExecuteProcedureScript($"SET FOREIGN_KEY_CHECKS = {(enabled ? 1 : 0)};");
        }

        #endregion
    }
}