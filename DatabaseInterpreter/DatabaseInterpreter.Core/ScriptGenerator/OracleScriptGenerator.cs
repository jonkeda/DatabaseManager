using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class OracleScriptGenerator : DbScriptGenerator
    {
        public OracleScriptGenerator(DbInterpreter dbInterpreter) : base(dbInterpreter)
        {
        }

        #region Schema Script

        public override ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo)
        {
            var sb = new ScriptBuilder();

            var dbSchema = GetDbSchema();

            #region User Defined Type

            foreach (var userDefinedType in schemaInfo.UserDefinedTypes)
            {
                FeedbackInfo(OperationState.Begin, userDefinedType);

                sb.AppendLine(CreateUserDefinedType(userDefinedType));

                FeedbackInfo(OperationState.End, userDefinedType);
            }

            #endregion

            #region Sequence

            foreach (var sequence in schemaInfo.Sequences)
            {
                FeedbackInfo(OperationState.Begin, sequence);

                sb.AppendLine(CreateSequence(sequence));

                FeedbackInfo(OperationState.End, sequence);
            }

            #endregion

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

            #region Procedure

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Procedures));

            #endregion

            if (option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                AppendScriptsToFile(sb.ToString(), GenerateScriptMode.Schema, true);

            return sb;
        }

        private string GetDbSchema()
        {
            return (dbInterpreter as OracleInterpreter).GetDbSchema();
        }

        #endregion

        #region Data Script

        public override async Task<string> GenerateDataScriptsAsync(SchemaInfo schemaInfo)
        {
            return await base.GenerateDataScriptsAsync(schemaInfo);
        }

        protected override string GetBatchInsertPrefix()
        {
            return "INSERT ALL INTO";
        }

        protected override string GetBatchInsertItemBefore(string tableName, string columnNames, bool isFirstRow)
        {
            return isFirstRow
                ? ""
                : $"INTO {tableName}{(string.IsNullOrEmpty(columnNames) ? "" : $"({columnNames})")} VALUES";
        }

        protected override string GetBatchInsertItemEnd(bool isAllEnd)
        {
            return isAllEnd ? $"{Environment.NewLine}SELECT 1 FROM DUAL;" : "";
        }

        protected override bool NeedInsertParameter(TableColumn column, object value)
        {
            if (value != null)
            {
                var type = value.GetType();
                var dataType = column.DataType.ToLower();

                if (dataType == "clob") return true;

                if (type == typeof(string))
                {
                    var str = value.ToString();

                    if (str.Length > 1000 || (str.Contains(OracleInterpreter.SEMICOLON_FUNC) && str.Length > 500))
                        return true;
                }
                else if (type.Name == nameof(TimeSpan))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Alter Table

        public override Script RenameTable(Table table, string newName)
        {
            return new AlterDbObjectScript<Table>(
                $"RENAME {GetQuotedFullTableName(table)} TO {GetQuotedString(newName)};");
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
                $"ALTER TABLE {GetQuotedString(table.Name)} ADD {dbInterpreter.ParseColumn(table, column)};");
        }

        public override Script RenameTableColumn(Table table, TableColumn column, string newName)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedString(table.Name)} RENAME COLUMN {GetQuotedString(column.Name)} TO {GetQuotedString(newName)};");
        }

        public override Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn)
        {
            var clause = dbInterpreter.ParseColumn(table, newColumn);

            if (DataTypeHelper.IsGeometryType(newColumn.DataType))
                clause = clause.Replace(dbInterpreter.ParseDataType(newColumn), "");

            return new AlterDbObjectScript<TableColumn>($"ALTER TABLE {GetQuotedString(table.Name)} MODIFY {clause}");
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
            var tablespace = dbInterpreter.ConnectionInfo.Database;
            var strTablespace = string.IsNullOrEmpty(tablespace) ? "" : $"TABLESPACE {tablespace}";
            var pkName = string.IsNullOrEmpty(primaryKey.Name)
                ? GetQuotedString($"PK_{primaryKey.TableName}")
                : GetQuotedString(primaryKey.Name);

            var sql =
                $@"
ALTER TABLE {GetQuotedFullTableName(primaryKey)} ADD CONSTRAINT {pkName} PRIMARY KEY 
(
{string.Join(Environment.NewLine, primaryKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)},")).TrimEnd(',')}
)
USING INDEX 
{strTablespace}{scriptsDelimiter}";

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
REFERENCES {GetQuotedString(foreignKey.ReferencedTableName)}({referenceColumnName})");

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

            var type = "";

            if (index.Type == IndexType.Unique.ToString())
                type = "UNIQUE";
            else if (index.Type == IndexType.Bitmap.ToString()) type = "BITMAP";

            var reverse = index.Type == IndexType.Reverse.ToString() ? "REVERSE" : "";

            return new CreateDbObjectScript<TableIndex>(
                $"CREATE {type} INDEX {indexName} ON {GetQuotedFullTableName(index)} ({columnNames}){reverse};");
        }

        public override Script DropIndex(TableIndex index)
        {
            return new DropDbObjectScript<TableIndex>($"DROP INDEX {GetQuotedString(index.Name)};");
        }

        public override Script AddCheckConstraint(TableConstraint constraint)
        {
            var ckName = string.IsNullOrEmpty(constraint.Name)
                ? GetQuotedString($"CK_{constraint.TableName}")
                : GetQuotedString(constraint.Name);

            return new CreateDbObjectScript<TableConstraint>(
                $"ALTER TABLE {GetQuotedFullTableName(constraint)} ADD CONSTRAINT {ckName} CHECK ({constraint.Definition});");
        }

        private Script AddUniqueConstraint(TableIndex index)
        {
            var columnNames = string.Join(",", index.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));

            return new CreateDbObjectScript<TableConstraint>(
                $"ALTER TABLE {GetQuotedFullTableName(index)} ADD CONSTRAINT {GetQuotedString(index.Name)} UNIQUE ({columnNames});");
        }

        public override Script DropCheckConstraint(TableConstraint constraint)
        {
            return new DropDbObjectScript<TableConstraint>(GetDropConstraintSql(constraint));
        }

        public override Script SetIdentityEnabled(TableColumn column, bool enabled)
        {
            return new Script("");
        }

        #endregion

        #region Database Operation

        public override Script CreateSchema(DatabaseSchema schema)
        {
            return new Script("");
        }

        public override Script CreateUserDefinedType(UserDefinedType userDefinedType)
        {
            var dataTypes = string.Join(",",
                userDefinedType.Attributes.Select(item =>
                    $"{GetQuotedString(item.Name)} {dbInterpreter.ParseDataType(new TableColumn { MaxLength = item.MaxLength, DataType = item.DataType, Precision = item.Precision, Scale = item.Scale })}"));

            var script = $"CREATE TYPE {GetQuotedString(userDefinedType.Name)} AS OBJECT ({dataTypes})" +
                         dbInterpreter.ScriptsDelimiter;

            return new CreateDbObjectScript<UserDefinedType>(script);
        }

        public override Script CreateSequence(Sequence sequence)
        {
            var script =
                $@"CREATE SEQUENCE {GetQuotedString(sequence.Name)}
START WITH {sequence.StartValue}
INCREMENT BY {sequence.Increment}
MINVALUE {sequence.MinValue}
MAXVALUE {sequence.MaxValue} 
{(sequence.CacheSize > 1 ? $"CACHE {sequence.CacheSize}" : "")}
{(sequence.Cycled ? "CYCLE" : "")}
{(sequence.Ordered ? "ORDER" : "")};";

            return new CreateDbObjectScript<Sequence>(script);
        }

        public override ScriptBuilder CreateTable(Table table, IEnumerable<TableColumn> columns,
            TablePrimaryKey primaryKey,
            IEnumerable<TableForeignKey> foreignKeys,
            IEnumerable<TableIndex> indexes,
            IEnumerable<TableConstraint> constraints)
        {
            var sb = new ScriptBuilder();

            var tableName = table.Name;
            var quotedTableName = GetQuotedFullTableName(table);

            var tablespace = dbInterpreter.ConnectionInfo.Database;
            var strTablespace = string.IsNullOrEmpty(tablespace) ? "" : $"TABLESPACE {tablespace}";

            #region Create Table

            var option = GetCreateTableOption();

            var tableScript =
                $@"
CREATE TABLE {quotedTableName}(
{string.Join("," + Environment.NewLine, columns.Select(item => dbInterpreter.ParseColumn(table, item))).TrimEnd(',')}
){strTablespace}" + (string.IsNullOrEmpty(option) ? "" : Environment.NewLine + option) + scriptsDelimiter;

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

                var primaryKeyColumnNames = primaryKey?.Columns?.OrderBy(item => item.ColumnName)
                    ?.Select(item => item.ColumnName);

                foreach (var index in indexes)
                {
                    var indexColumnNames =
                        index.Columns.OrderBy(item => item.ColumnName).Select(item => item.ColumnName);

                    //primary key column can't be indexed twice if they have same name and same order
                    if (primaryKeyColumnNames != null && primaryKeyColumnNames.SequenceEqual(indexColumnNames))
                        continue;

                    var strIndexColumnNames = string.Join(",", indexColumnNames);

                    //Avoid duplicated indexes for one index.
                    if (indexColumns.Contains(strIndexColumnNames)) continue;

                    if (index.Type == nameof(IndexType.Unique) || index.IsUnique)
                        //create a constraint, if the column has foreign key, it's required.
                        sb.AppendLine(AddUniqueConstraint(index));
                    else
                        sb.AppendLine(AddIndex(index));

                    if (!indexColumns.Contains(strIndexColumnNames)) indexColumns.Add(strIndexColumnNames);
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
            var isTable = dbObject is Table;

            return
                $@"DROP {typeName.ToUpper()} {GetQuotedDbObjectNameWithSchema(dbObject)}{(isTable ? " PURGE" : "")};";
        }

        public override IEnumerable<Script> SetConstrainsEnabled(bool enabled)
        {
            var sqls = new List<string> { GetSqlForEnableConstraints(enabled), GetSqlForEnableTrigger(enabled) };
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
        }

        private string GetSqlForEnableConstraints(bool enabled)
        {
            return
                $@"SELECT 'ALTER TABLE ""'|| T.TABLE_NAME ||'"" {(enabled ? "ENABLE" : "DISABLE")} CONSTRAINT ""'||T.CONSTRAINT_NAME || '""' AS ""SQL""  
                            FROM USER_CONSTRAINTS T 
                            WHERE T.CONSTRAINT_TYPE = 'R'
                            AND UPPER(OWNER)= UPPER('{GetDbSchema()}')
                           ";
        }

        private string GetSqlForEnableTrigger(bool enabled)
        {
            return $@"SELECT 'ALTER TRIGGER ""'|| TRIGGER_NAME || '"" {(enabled ? "ENABLE" : "DISABLE")} '
                         FROM USER_TRIGGERS
                         WHERE UPPER(TABLE_OWNER)= UPPER('{GetDbSchema()}')";
        }

        #endregion
    }
}