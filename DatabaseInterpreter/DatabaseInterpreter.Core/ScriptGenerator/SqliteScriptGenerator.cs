using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class SqliteScriptGenerator : DbScriptGenerator
    {
        public SqliteScriptGenerator(DbInterpreter dbInterpreter) : base(dbInterpreter)
        {
        }

        #region Schema Script

        public override ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo)
        {
            var sb = new ScriptBuilder();

            #region Table

            foreach (var table in schemaInfo.Tables)
            {
                FeedbackInfo(OperationState.Begin, table);

                IEnumerable<TableColumn> columns = schemaInfo.TableColumns
                    .Where(item => item.Schema == table.Schema && item.TableName == table.Name)
                    .OrderBy(item => item.Order);
                var primaryKey = schemaInfo.TablePrimaryKeys.FirstOrDefault(item =>
                    item.Schema == table.Schema && item.TableName == table.Name);
                var foreignKeys = schemaInfo.TableForeignKeys.Where(item =>
                    item.Schema == table.Schema && item.TableName == table.Name);
                IEnumerable<TableIndex> indexes = schemaInfo.TableIndexes
                    .Where(item => item.Schema == table.Schema && item.TableName == table.Name)
                    .OrderBy(item => item.Order);
                var constraints = schemaInfo.TableConstraints.Where(item =>
                    item.Schema == table.Schema && item.TableName == table.Name);

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

            if (option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                AppendScriptsToFile(sb.ToString().Trim(), GenerateScriptMode.Schema, true);

            return sb;
        }

        #endregion

        #region Alter Table

        public override Script DropCheckConstraint(TableConstraint constraint)
        {
            return new Script("");
        }

        public override Script DropForeignKey(TableForeignKey foreignKey)
        {
            return new Script("");
        }

        public override Script DropIndex(TableIndex index)
        {
            return new DropDbObjectScript<TableIndex>($"DROP INDEX IF EXISTS {GetQuotedString(index.Name)};");
        }

        public override Script DropPrimaryKey(TablePrimaryKey primaryKey)
        {
            return new Script("");
        }

        public override Script DropTableColumn(TableColumn column)
        {
            return new DropDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(column.TableName)} DROP COLUMN {GetQuotedString(column.Name)};");
        }

        public override Script DropTrigger(TableTrigger trigger)
        {
            return new DropDbObjectScript<View>(GetDropSql(nameof(DatabaseObjectType.Trigger), trigger));
        }

        private string GetDropSql(string typeName, DatabaseObject dbObject)
        {
            return $"DROP {typeName.ToUpper()} IF EXISTS {GetQuotedDbObjectNameWithSchema(dbObject)};";
        }

        public override Script RenameTable(Table table, string newName)
        {
            return new AlterDbObjectScript<Table>(
                $"ALTER TABLE {GetQuotedString(table.Name)} RENAME TO {GetQuotedString(newName)};");
        }

        public override Script RenameTableColumn(Table table, TableColumn column, string newName)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} CHANGE {GetQuotedString(column.Name)} {newName} {dbInterpreter.ParseDataType(column)};");
        }

        public override IEnumerable<Script> SetConstrainsEnabled(bool enabled)
        {
            return Enumerable.Empty<Script>();
        }

        public override Script SetIdentityEnabled(TableColumn column, bool enabled)
        {
            return new Script("");
        }

        public override Script SetTableColumnComment(Table table, TableColumn column, bool isNew = true)
        {
            return new Script("");
        }

        public override Script SetTableComment(Table table, bool isNew = true)
        {
            return new Script("");
        }

        public override Script AddCheckConstraint(TableConstraint constraint)
        {
            return new Script("");
        }

        public override Script AddForeignKey(TableForeignKey foreignKey)
        {
            return new Script("");
        }

        public override Script AddIndex(TableIndex index)
        {
            var columnNames = string.Join(",", index.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));

            var type = "";

            if (index.Type == IndexType.Unique.ToString()) type = "UNIQUE";

            var sql =
                $"CREATE {type} INDEX {GetQuotedString(index.Name)} ON {GetQuotedString(index.TableName)}({columnNames})";

            return new CreateDbObjectScript<TableIndex>(sql + scriptsDelimiter);
        }

        public override Script AddPrimaryKey(TablePrimaryKey primaryKey)
        {
            return new Script("");
        }

        public override Script AddTableColumn(Table table, TableColumn column)
        {
            return new CreateDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} ADD {dbInterpreter.ParseColumn(table, column)};");
        }

        public override Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn)
        {
            return new Script("");
        }

        #endregion

        #region Database Operation

        public override ScriptBuilder CreateTable(Table table, IEnumerable<TableColumn> columns,
            TablePrimaryKey primaryKey, IEnumerable<TableForeignKey> foreignKeys, IEnumerable<TableIndex> indexes,
            IEnumerable<TableConstraint> constraints)
        {
            var sb = new ScriptBuilder();

            var quotedTableName = GetQuotedFullTableName(table);

            var tableOption = GetCreateTableOption();

            #region Create Table

            var existsClause = dbInterpreter.NotCreateIfExists ? " IF NOT EXISTS " : "";

            #region Primary Key

            var primaryKeyConstraint = "";

            if (this.option.TableScriptsGenerateOption.GeneratePrimaryKey && primaryKey != null)
            {
                var primaryKeyName = primaryKey.Name ?? "";

                primaryKeyConstraint =
                    $@"
,CONSTRAINT {primaryKeyName} PRIMARY KEY
 (
  {string.Join(Environment.NewLine, primaryKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)},")).TrimEnd(',')}
 )";
            }

            #endregion

            #region Foreign Key

            var foreginKeyConstraint = new StringBuilder();

            if (this.option.TableScriptsGenerateOption.GenerateForeignKey && foreignKeys != null)
                foreach (var foreignKey in foreignKeys)
                {
                    var columnNames = string.Join(",",
                        foreignKey.Columns.Select(item => GetQuotedString(item.ColumnName)));
                    var referenceColumnName = string.Join(",",
                        foreignKey.Columns.Select(item => $"{GetQuotedString(item.ReferencedColumnName)}"));

                    var foreignKeyName = GetQuotedString(foreignKey.Name) ?? "";

                    var sbForeignKeyItem = new StringBuilder();

                    var fkConstraint = "";

                    if (!string.IsNullOrEmpty(foreignKeyName)) fkConstraint = $"CONSTRAINT {foreignKeyName} ";

                    sbForeignKeyItem.Append(
                        $",{fkConstraint}FOREIGN KEY ({columnNames}) REFERENCES {GetQuotedString(foreignKey.ReferencedTableName)}({referenceColumnName})");

                    if (foreignKey.UpdateCascade)
                        sbForeignKeyItem.Append(" ON UPDATE CASCADE");
                    else
                        sbForeignKeyItem.Append(" ON UPDATE NO ACTION");

                    if (foreignKey.DeleteCascade)
                        sbForeignKeyItem.Append(" ON DELETE CASCADE");
                    else
                        sbForeignKeyItem.Append(" ON DELETE NO ACTION");

                    foreginKeyConstraint.AppendLine(sbForeignKeyItem.ToString());
                }

            #endregion

            #region Index

            var useColumnIndex = false;

            if (this.option.TableScriptsGenerateOption.GenerateIndex && indexes != null)
                if (indexes.All(item => item.Columns.Count == 1 && item.IsUnique))
                    useColumnIndex = true;

            #endregion

            #region Constraint

            var useColumnCheckConstraint = false;

            if (this.option.TableScriptsGenerateOption.GenerateConstraint && constraints != null)
                useColumnCheckConstraint = true;

            #endregion

            var columnItems = new List<string>();

            foreach (var column in columns)
            {
                var parsedColumn = dbInterpreter.ParseColumn(table, column).Trim(';');

                if (useColumnIndex)
                {
                    var index = indexes.FirstOrDefault(item =>
                        item.IsUnique && item.Columns.Any(t => t.ColumnName == column.Name));

                    if (index != null)
                        parsedColumn += $"{Environment.NewLine}CONSTRAINT {GetQuotedString(index.Name) ?? ""} UNIQUE";
                }

                if (useColumnCheckConstraint)
                {
                    var checkConstraint = constraints.FirstOrDefault(item => item.ColumnName == column.Name);

                    if (checkConstraint != null)
                        parsedColumn +=
                            $"{Environment.NewLine}CONSTRAINT {GetQuotedString(checkConstraint.Name) ?? ""} CHECK ({checkConstraint.Definition})";
                }

                columnItems.Add(parsedColumn);
            }

            var tableScript =
                $@"CREATE TABLE{existsClause} {quotedTableName}(
{string.Join("," + Environment.NewLine, columnItems)}{primaryKeyConstraint}{(foreginKeyConstraint.Length > 0 ? Environment.NewLine : "")}{foreginKeyConstraint.ToString().Trim()}
){tableOption};";

            sb.AppendLine(new CreateDbObjectScript<Table>(tableScript));

            #endregion

            #region Index

            if (this.option.TableScriptsGenerateOption.GenerateIndex && indexes != null && !useColumnIndex)
                foreach (var index in indexes)
                    sb.AppendLine(AddIndex(index));

            #endregion

            return sb;
        }

        public override Script CreateSchema(DatabaseSchema schema)
        {
            return new Script("");
        }

        public override Script CreateSequence(Sequence sequence)
        {
            return new Script("");
        }

        public override Script CreateUserDefinedType(UserDefinedType userDefinedType)
        {
            return new Script("");
        }

        public override Script DropTable(Table table)
        {
            return new DropDbObjectScript<Table>(GetDropSql(nameof(DatabaseObjectType.Table), table));
        }

        public override Script DropSequence(Sequence sequence)
        {
            return new Script("");
        }

        public override Script DropProcedure(Procedure procedure)
        {
            return new Script("");
        }

        public override Script DropFunction(Function function)
        {
            return new Script("");
        }

        public override Script DropUserDefinedType(UserDefinedType userDefinedType)
        {
            return new Script("");
        }

        public override Script DropView(View view)
        {
            return new DropDbObjectScript<Table>(GetDropSql(nameof(DatabaseObjectType.View), view));
        }

        #endregion
    }
}