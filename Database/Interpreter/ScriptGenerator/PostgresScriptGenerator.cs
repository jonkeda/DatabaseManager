using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class PostgresScriptGenerator : DbScriptGenerator
    {
        public PostgresScriptGenerator(DbInterpreter dbInterpreter) : base(dbInterpreter)
        {
        }

        public string NotCreateIfExistsClause => dbInterpreter.NotCreateIfExists ? "IF NOT EXISTS" : "";

        #region Schema Script

        public override ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo)
        {
            var sb = new ScriptBuilder();

            #region User Defined Type

            foreach (var userDefinedType in schemaInfo.UserDefinedTypes)
            {
                FeedbackInfo(OperationState.Begin, userDefinedType);

                sb.AppendLine(CreateUserDefinedType(userDefinedType));

                FeedbackInfo(OperationState.End, userDefinedType);
            }

            #endregion

            #region Function

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Functions));

            #endregion

            #region Sequence

            foreach (var sequence in schemaInfo.Sequences)
            {
                FeedbackInfo(OperationState.Begin, sequence);

                sb.AppendLine(CreateSequence(sequence));

                FeedbackInfo(OperationState.End, sequence);
            }

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
                var constraints = schemaInfo.TableConstraints.Where(item =>
                    item.Schema == table.Schema && item.TableName == table.Name);

                foreach (var index in schemaInfo.TableIndexes)
                {
                    var indexName = index.Name;

                    if (schemaInfo.Tables.Any(item => item.Name == indexName))
                    {
                        var columnNames = string.Join("_", index.Columns.Select(item => item.ColumnName));

                        index.Name = $"IX_{index.TableName}_{columnNames}";
                    }
                }

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

        #endregion

        #region Data Script

        protected override bool NeedInsertParameter(TableColumn column, object value)
        {
            if (value != null)
            {
                var dataType = column.DataType.ToLower();
                if (dataType == "bytea" || dataType == "bit" || dataType == "bit varying") return true;
            }

            return false;
        }

        #endregion

        #region Alter Table

        public override Script RenameTable(Table table, string newName)
        {
            return new AlterDbObjectScript<Table>(
                $"ALTER TABLE {GetQuotedDbObjectNameWithSchema(table)} RENAME TO {GetQuotedString(newName)};");
        }

        public override Script SetTableComment(Table table, bool isNew = true)
        {
            return new AlterDbObjectScript<Table>(
                $"COMMENT ON TABLE {GetQuotedFullTableName(table)} IS '{dbInterpreter.ReplaceSplitChar(TransferSingleQuotationString(table.Comment))}'" +
                scriptsDelimiter);
        }

        public override Script AddTableColumn(Table table, TableColumn column)
        {
            return new CreateDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedFullTableName(table)} ADD {dbInterpreter.ParseColumn(table, column)};");
        }

        public override Script RenameTableColumn(Table table, TableColumn column, string newName)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedFullTableName(table)} RENAME {GetQuotedString(column.Name)} TO {GetQuotedString(newName)};");
        }

        public override Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn)
        {
            var alter = "";

            var newDataType = dbInterpreter.ParseDataType(newColumn);
            var oldDataType = dbInterpreter.ParseDataType(oldColumn);

            if (!string.IsNullOrEmpty(newColumn.ComputeExp))
            {
                alter = DropTableColumn(oldColumn).Content;

                alter += $"{Environment.NewLine}{AddTableColumn(table, newColumn).Content}";

                return new AlterDbObjectScript<TableColumn>(alter);
            }

            if (newDataType != oldDataType) alter = $"TYPE {newDataType}";

            if (newColumn.IsNullable && !oldColumn.IsNullable) alter = "DROP NOT NULL";

            if (!newColumn.IsNullable && oldColumn.IsNullable) alter = "SET NOT NULL";

            if (!string.IsNullOrEmpty(newColumn.DefaultValue) && string.IsNullOrEmpty(oldColumn.DefaultValue))
                alter = $"SET DEFAULT {newColumn.DefaultValue}";

            if (string.IsNullOrEmpty(newColumn.DefaultValue) && !string.IsNullOrEmpty(oldColumn.DefaultValue))
                alter = "DROP DEFAULT";

            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} ALTER COLUMN {GetQuotedString(newColumn.Name)} {alter};");
        }

        public override Script SetTableColumnComment(Table table, TableColumn column, bool isNew = true)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"COMMENT ON COLUMN {GetQuotedFullTableChildName(column)} IS '{dbInterpreter.ReplaceSplitChar(TransferSingleQuotationString(column.Comment))}'" +
                scriptsDelimiter);
        }

        public override Script DropTableColumn(TableColumn column)
        {
            return new DropDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(column.TableName)} DROP COLUMN {GetQuotedString(column.Name)};");
        }

        public override Script AddPrimaryKey(TablePrimaryKey primaryKey)
        {
            var pkName = string.IsNullOrEmpty(primaryKey.Name)
                ? GetQuotedString($"PK_{primaryKey.TableName}")
                : GetQuotedString(primaryKey.Name);

            var sql =
                $@"
ALTER TABLE {GetQuotedFullTableName(primaryKey)} ADD CONSTRAINT {pkName} PRIMARY KEY 
(
{string.Join(Environment.NewLine, primaryKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)},")).TrimEnd(',')}
);";

            return new Script(sql);
        }

        public override Script DropPrimaryKey(TablePrimaryKey primaryKey)
        {
            return new DropDbObjectScript<TablePrimaryKey>(GetDropConstraintSql(primaryKey));
        }

        public override Script AddForeignKey(TableForeignKey foreignKey)
        {
            var columnNames =
                string.Join(",", foreignKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));
            var referenceColumnName = string.Join(",",
                foreignKey.Columns.Select(item => $"{GetQuotedString(item.ReferencedColumnName)}"));

            var fkName = string.IsNullOrEmpty(foreignKey.Name)
                ? GetQuotedString($"FK_{foreignKey.TableName}_{foreignKey.ReferencedTableName}")
                : GetQuotedString(foreignKey.Name);

            var sb = new StringBuilder();

            sb.AppendLine(
                $@"
ALTER TABLE {GetQuotedFullTableName(foreignKey)} ADD CONSTRAINT {fkName} FOREIGN KEY ({columnNames})
REFERENCES {GetQuotedDbObjectNameWithSchema(foreignKey.ReferencedSchema, foreignKey.ReferencedTableName)}({referenceColumnName})");

            if (foreignKey.UpdateCascade) sb.AppendLine("ON UPDATE CASCADE");

            if (foreignKey.DeleteCascade) sb.AppendLine("ON DELETE CASCADE");

            sb.Append(scriptsDelimiter);

            return new CreateDbObjectScript<TableForeignKey>(sb.ToString());
        }

        public override Script DropForeignKey(TableForeignKey foreignKey)
        {
            return new DropDbObjectScript<TableForeignKey>(GetDropConstraintSql(foreignKey));
        }

        private string GetDropConstraintSql(TableChild tableChild)
        {
            return
                $"ALTER TABLE {GetQuotedFullTableName(tableChild)} DROP CONSTRAINT {GetQuotedString(tableChild.Name)};";
        }

        public override Script AddIndex(TableIndex index)
        {
            var columnNames = string.Join(",", index.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));

            var indexName = string.IsNullOrEmpty(index.Name)
                ? GetQuotedString($"IX_{index.TableName}")
                : GetQuotedString(index.Name);

            var type = index.Type;
            var indexType = IndexType.None;

            foreach (var name in Enum.GetNames(typeof(IndexType)))
                if (name.ToUpper() == type?.ToUpper())
                {
                    indexType = (IndexType)Enum.Parse(typeof(IndexType), name);
                    break;
                }

            if (indexType == IndexType.None || (indexType | dbInterpreter.IndexType) != dbInterpreter.IndexType)
                indexType = IndexType.BTree;

            var sql = "";

            Action addNormOrUnique = () =>
            {
                if (type == IndexType.Unique.ToString())
                    //use unique constraint, it can be used for foreign key reference.
                    sql =
                        $"ALTER TABLE {GetQuotedFullTableName(index)} ADD CONSTRAINT {indexName} UNIQUE ({columnNames});";
                else
                    sql =
                        $"CREATE INDEX {GetQuotedString(index.Name)} ON {GetQuotedFullTableName(index)}({columnNames});";
            };

            if (indexType == IndexType.Unique)
            {
                addNormOrUnique();
            }
            else if (type != IndexType.Unique.ToString())
            {
                if ((indexType | dbInterpreter.IndexType) == dbInterpreter.IndexType)
                    sql =
                        $"CREATE INDEX {GetQuotedString(index.Name)} ON {GetQuotedFullTableName(index)} USING {indexType.ToString().ToUpper()}({columnNames});";
                else
                    addNormOrUnique();
            }

            return new CreateDbObjectScript<TableIndex>(sql);
        }

        public override Script DropIndex(TableIndex index)
        {
            if (index.Type == IndexType.Unique.ToString())
                return new CreateDbObjectScript<TableIndex>(
                    $"ALTER TABLE IF EXISTS {GetQuotedFullTableName(index)} DROP CONSTRAINT  {GetQuotedString(index.Name)};");
            return new DropDbObjectScript<TableIndex>($"DROP INDEX {GetQuotedString(index.Name)};");
        }

        public override Script AddCheckConstraint(TableConstraint constraint)
        {
            var ckName = string.IsNullOrEmpty(constraint.Name)
                ? GetQuotedString($"CK_{constraint.Name}")
                : GetQuotedString(constraint.Name);

            return new CreateDbObjectScript<TableConstraint>(
                $"ALTER TABLE IF EXISTS {GetQuotedFullTableName(constraint)} ADD CONSTRAINT {ckName} CHECK {constraint.Definition};");
        }

        public override Script DropCheckConstraint(TableConstraint constraint)
        {
            return new DropDbObjectScript<TableConstraint>(GetDropConstraintSql(constraint));
        }

        public override Script SetIdentityEnabled(TableColumn column, bool enabled)
        {
            return new Script(
                $"ALTER TABLE {GetQuotedFullTableName(column)} ALTER COLUMN {GetQuotedString(column.Name)} {(enabled ? "ADD" : "DROP")} IDENTITY IF EXISTS");
        }

        #endregion

        #region Database Operation

        public override Script CreateSchema(DatabaseSchema schema)
        {
            var script = $"CREATE SCHEMA IF NOT EXISTS {GetQuotedString(schema.Name)};";

            return new CreateDbObjectScript<DatabaseSchema>(script);
        }

        public override Script CreateUserDefinedType(UserDefinedType userDefinedType)
        {
            var dataTypes = string.Join(",",
                userDefinedType.Attributes.Select(item =>
                    $"{GetQuotedString(item.Name)} {dbInterpreter.ParseDataType(new TableColumn { MaxLength = item.MaxLength, DataType = item.DataType, Precision = item.Precision, Scale = item.Scale })}"));

            var script = $"CREATE TYPE {GetQuotedString(userDefinedType.Name)} AS ({dataTypes})" +
                         dbInterpreter.ScriptsDelimiter;

            return new CreateDbObjectScript<UserDefinedType>(script);
        }

        public override Script CreateSequence(Sequence sequence)
        {
            var script =
                $@"CREATE SEQUENCE IF NOT EXISTS {GetQuotedDbObjectNameWithSchema(sequence)}
START {sequence.StartValue}
INCREMENT {sequence.Increment}
MINVALUE {(long)sequence.MinValue}
MAXVALUE {(long)sequence.MaxValue}
{(sequence.Cycled ? "CYCLE" : "")}
{(sequence.CacheSize > 0 ? $"CACHE {sequence.CacheSize}" : "")}
{(sequence.OwnedByTable == null ? "" : $"OWNED BY {GetQuotedString(sequence.OwnedByTable)}.{GetQuotedString(sequence.OwnedByColumn)}")};";

            return new CreateDbObjectScript<Sequence>(script);
        }

        public override ScriptBuilder CreateTable(Table table, IEnumerable<TableColumn> columns,
            TablePrimaryKey primaryKey,
            IEnumerable<TableForeignKey> foreignKeys,
            IEnumerable<TableIndex> indexes,
            IEnumerable<TableConstraint> constraints)
        {
            var isLowDbVersion = dbInterpreter.IsLowDbVersion();

            var sb = new ScriptBuilder();

            var quotedTableName = GetQuotedFullTableName(table);

            #region Create Table

            var option = GetCreateTableOption();

            var tableScript =
                $@"
CREATE TABLE {NotCreateIfExistsClause} {quotedTableName}(
{string.Join("," + Environment.NewLine, columns.Select(item => dbInterpreter.ParseColumn(table, item))).TrimEnd(',')}
){(isLowDbVersion ? "" : option)};";

            sb.AppendLine(new CreateDbObjectScript<Table>(tableScript));

            #endregion

            sb.AppendLine();

            #region Comment

            if (this.option.TableScriptsGenerateOption.GenerateComment)
            {
                if (!string.IsNullOrEmpty(table.Comment)) sb.AppendLine(SetTableComment(table));

                foreach (var column in columns.Where(item => !string.IsNullOrEmpty(item.Comment)))
                    sb.AppendLine(SetTableColumnComment(table, column));
            }

            #endregion

            #region Primary Key

            if (this.option.TableScriptsGenerateOption.GeneratePrimaryKey && primaryKey != null)
                sb.AppendLine(AddPrimaryKey(primaryKey));

            #endregion

            #region Foreign Key

            if (this.option.TableScriptsGenerateOption.GenerateForeignKey && foreignKeys != null)
                foreach (var foreignKey in foreignKeys)
                    sb.AppendLine(AddForeignKey(foreignKey));

            #endregion

            #region Index

            if (this.option.TableScriptsGenerateOption.GenerateIndex && indexes != null)
            {
                var indexColumns = new List<string>();

                foreach (var index in indexes)
                {
                    var columnNames = string.Join(",",
                        index.Columns.OrderBy(item => item.ColumnName).Select(item => item.ColumnName));

                    //Avoid duplicated indexes for one index.
                    if (indexColumns.Contains(columnNames)) continue;

                    sb.AppendLine(AddIndex(index));

                    if (!indexColumns.Contains(columnNames)) indexColumns.Add(columnNames);
                }
            }

            #endregion

            #region Constraint

            if (this.option.TableScriptsGenerateOption.GenerateConstraint && constraints != null)
                foreach (var constraint in constraints)
                    sb.AppendLine(AddCheckConstraint(constraint));

            #endregion

            return sb;
        }

        public override Script DropUserDefinedType(UserDefinedType userDefinedType)
        {
            return new DropDbObjectScript<UserDefinedType>(GetDropSql(nameof(DatabaseObjectType.Type),
                userDefinedType));
        }

        public override Script DropSequence(Sequence sequence)
        {
            return new DropDbObjectScript<Sequence>(GetDropSql(nameof(DatabaseObjectType.Sequence), sequence));
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
            var cascadeOption = typeName == nameof(Table) || typeName == nameof(View) || typeName == nameof(Function)
                                || typeName == nameof(Procedure)
                ? " CASCADE"
                : "";

            return $"DROP {typeName.ToUpper()} IF EXISTS {GetQuotedDbObjectNameWithSchema(dbObject)}{cascadeOption};";
        }

        public override IEnumerable<Script> SetConstrainsEnabled(bool enabled)
        {
            var sqls = new List<string> { GetSqlForEnableTrigger(enabled) };
            var cmds = new List<string>();

            using (var dbConnection = dbInterpreter.CreateConnection())
            {
                foreach (var sql in sqls)
                {
                    var reader = dbInterpreter.GetDataReader(dbConnection, sql);

                    while (reader.Read())
                    {
                        var cmd = reader[0].ToString();
                        cmds.Add(cmd);
                    }
                }

                foreach (var cmd in cmds) yield return new Script(cmd);
            }

            yield return new Script(GetSqlForEnableConstraints(enabled));
        }

        private string GetSqlForEnableConstraints(bool enabled)
        {
            return $"SET session_replication_role = {(enabled ? "default" : "replica")};";
        }

        private string GetSqlForEnableTrigger(bool enabled)
        {
            return
                $@"SELECT CONCAT('ALTER TABLE ',event_object_schema,'.""',event_object_table,'"" {(enabled ? "ENABLE" : "DISABLE")} TRIGGER ALL;')
                      FROM information_schema.triggers
                      WHERE UPPER(trigger_catalog)= UPPER('{dbInterpreter.ConnectionInfo.Database}')";
        }

        #endregion
    }
}