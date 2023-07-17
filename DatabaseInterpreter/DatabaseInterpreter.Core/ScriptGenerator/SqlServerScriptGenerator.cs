using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class SqlServerScriptGenerator : DbScriptGenerator
    {
        public SqlServerScriptGenerator(DbInterpreter dbInterpreter) : base(dbInterpreter)
        {
        }

        #region Schema Script

        public override ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo)
        {
            var sb = new ScriptBuilder();

            #region User Defined Type

            foreach (var userDefinedType in schemaInfo.UserDefinedTypes)
            {
                FeedbackInfo(OperationState.Begin, userDefinedType);

                sb.AppendLine(CreateUserDefinedType(userDefinedType));
                sb.AppendLine(new SpliterScript(scriptsDelimiter));

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

            #region Procedure

            sb.AppendRange(GenerateScriptDbObjectScripts(schemaInfo.Procedures));

            #endregion

            if (option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                AppendScriptsToFile(sb.ToString().Trim(), GenerateScriptMode.Schema, true);

            return sb;
        }

        private bool IsBigDataType(TableColumn column)
        {
            switch (column.DataType)
            {
                case "text":
                case "ntext":
                case "image":
                case "xml":
                    return true;
                case "varchar":
                case "nvarchar":
                case "varbinary":
                    if (column.MaxLength == -1 && !column.IsComputed) return true;
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        #region Data Script

        public override Task<string> GenerateDataScriptsAsync(SchemaInfo schemaInfo)
        {
            return base.GenerateDataScriptsAsync(schemaInfo);
        }

        protected override string GetBytesConvertHexString(object value, string dataType)
        {
            var hex = ValueHelper.BytesToHexString(value as byte[]);
            return $"CAST({hex} AS {dataType})";
        }

        #endregion

        #region Alter Table

        public override Script RenameTable(Table table, string newName)
        {
            return new ExecuteProcedureScript(
                $"EXEC sp_rename '{GetQuotedDbObjectNameWithSchema(table)}', '{newName}'");
        }

        public override Script SetTableComment(Table table, bool isNew = true)
        {
            return new ExecuteProcedureScript(
                $"EXEC {(isNew ? "sp_addextendedproperty" : "sp_updateextendedproperty")} N'MS_Description',N'{dbInterpreter.ReplaceSplitChar(TransferSingleQuotationString(table.Comment))}',N'SCHEMA',N'{table.Schema}',N'table',N'{table.Name}',NULL,NULL");
        }

        public override Script AddTableColumn(Table table, TableColumn column)
        {
            return new CreateDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedDbObjectNameWithSchema(table)} ADD {dbInterpreter.ParseColumn(table, column)}");
        }

        public override Script RenameTableColumn(Table table, TableColumn column, string newName)
        {
            return new ExecuteProcedureScript(GetRenameScript(table.Schema, "COLUMN", table.Name, column.Name,
                newName));
        }

        public override Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn)
        {
            return new AlterDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedFullTableName(table)} ALTER COLUMN {dbInterpreter.ParseColumn(table, newColumn)}");
        }

        private string GetRenameScript(string schema, string type, string tableName, string name, string newName)
        {
            return
                $"EXEC sp_rename N'{schema}.{GetQuotedString(tableName)}.{GetQuotedString(name)}', N'{newName}', N'{type}'";
        }

        public override Script SetTableColumnComment(Table table, TableColumn column, bool isNew = true)
        {
            return SetTableChildComment(column, isNew);
        }

        public override Script DropTableColumn(TableColumn column)
        {
            return new DropDbObjectScript<TableColumn>(
                $"ALTER TABLE {GetQuotedFullTableName(column)} DROP COLUMN {GetQuotedString(column.Name)}");
        }

        public override Script AddPrimaryKey(TablePrimaryKey primaryKey)
        {
            var pkName = string.IsNullOrEmpty(primaryKey.Name)
                ? GetQuotedString($"PK_{primaryKey.TableName}")
                : GetQuotedString(primaryKey.Name);

            var script =
                $@"ALTER TABLE {GetQuotedFullTableName(primaryKey)} ADD CONSTRAINT
{pkName} PRIMARY KEY {(primaryKey.Clustered ? "CLUSTERED" : "NONCLUSTERED")}
(
    {string.Join(",", primaryKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)} {(item.IsDesc ? "DESC" : "")}"))}
) WITH(STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]";

            return new CreateDbObjectScript<TablePrimaryKey>(script);
        }

        public override Script DropPrimaryKey(TablePrimaryKey primaryKey)
        {
            return new DropDbObjectScript<TablePrimaryKey>(GetDropConstraintSql(primaryKey));
        }

        public override Script AddForeignKey(TableForeignKey foreignKey)
        {
            var quotedTableName = GetQuotedFullTableName(foreignKey);
            var fkName = string.IsNullOrEmpty(foreignKey.Name)
                ? GetQuotedString($"FK_{foreignKey.TableName}_{foreignKey.ReferencedTableName}")
                : GetQuotedString(foreignKey.Name);

            var columnNames =
                string.Join(",", foreignKey.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}"));
            var referencedColumnName = string.Join(",",
                foreignKey.Columns.Select(item => $"{GetQuotedString(item.ReferencedColumnName)}"));

            var sb = new StringBuilder();

            sb.AppendLine(
                $@"ALTER TABLE {quotedTableName} WITH CHECK ADD CONSTRAINT {fkName} FOREIGN KEY({columnNames})
REFERENCES {GetQuotedDbObjectNameWithSchema(foreignKey.ReferencedSchema, foreignKey.ReferencedTableName)} ({referencedColumnName})");

            if (foreignKey.UpdateCascade) sb.AppendLine("ON UPDATE CASCADE");

            if (foreignKey.DeleteCascade) sb.AppendLine("ON DELETE CASCADE");

            //sb.AppendLine($"ALTER TABLE {quotedTableName} CHECK CONSTRAINT {this.GetQuotedString(foreignKey.Name)}");

            return new CreateDbObjectScript<TableForeignKey>(sb.ToString());
        }

        public override Script DropForeignKey(TableForeignKey foreignKey)
        {
            return new DropDbObjectScript<TableForeignKey>(GetDropConstraintSql(foreignKey));
        }

        public override Script AddIndex(TableIndex index)
        {
            var columnNames = string.Join(",",
                index.Columns.Select(item => $"{GetQuotedString(item.ColumnName)}{(item.IsDesc ? " DESC" : "")}"));

            var unique = index.IsUnique ? "UNIQUE" : "";
            var clustered = index.Clustered ? "CLUSTERED" : "NONCLUSTERED";
            var type = index.Type == IndexType.ColumnStore.ToString() ? "COLUMNSTORE" : "";

            var sql =
                $@"CREATE {unique} {clustered} {type} INDEX {GetQuotedString(index.Name)} ON {GetQuotedFullTableName(index)}({columnNames})";

            return new CreateDbObjectScript<TableIndex>(sql);
        }

        public override Script DropIndex(TableIndex index)
        {
            return new DropDbObjectScript<TableIndex>(
                $"DROP INDEX {GetQuotedString(index.Name)} ON {GetQuotedFullTableName(index)}");
        }

        private string GetDropConstraintSql(TableChild tableChild)
        {
            return
                $"ALTER TABLE {GetQuotedFullTableName(tableChild)} DROP CONSTRAINT {GetQuotedString(tableChild.Name)}";
        }

        public override Script AddCheckConstraint(TableConstraint constraint)
        {
            var ckName = string.IsNullOrEmpty(constraint.Name)
                ? GetQuotedString($"CK_{constraint.TableName}")
                : GetQuotedString(constraint.Name);

            return new CreateDbObjectScript<TableConstraint>(
                $"ALTER TABLE {GetQuotedFullTableName(constraint)}  WITH CHECK ADD CONSTRAINT {ckName} CHECK  ({constraint.Definition})");
        }

        public override Script DropCheckConstraint(TableConstraint constraint)
        {
            return new DropDbObjectScript<TableConstraint>(GetDropConstraintSql(constraint));
        }

        public Script SetTableChildComment(TableChild tableChild, bool isNew)
        {
            var typeName = tableChild.GetType().Name;

            var type = "";

            if (typeName == nameof(TableColumn))
                type = "COLUMN";
            else if (typeName == nameof(TablePrimaryKey) || typeName == nameof(TableForeignKey) ||
                     typeName == nameof(TableConstraint))
                type = "CONSTRAINT";
            else if (typeName == nameof(TableIndex)) type = "INDEX";

            var sql =
                $"EXEC {(isNew ? "sp_addextendedproperty" : "sp_updateextendedproperty")} N'MS_Description',N'{TransferSingleQuotationString(tableChild.Comment)}',N'SCHEMA',N'{tableChild.Schema}',N'table',N'{tableChild.TableName}',N'{type}',N'{tableChild.Name}'";
            return new ExecuteProcedureScript(sql);
        }

        public Script AddDefaultValueConstraint(TableColumn column)
        {
            var defaultValue = StringHelper.GetParenthesisedString(dbInterpreter.GetColumnDefaultValue(column));

            return new AlterDbObjectScript<Table>(
                $"ALTER TABLE {GetQuotedFullTableName(column)} ADD CONSTRAINT {GetQuotedString($"DF_{column.TableName}_{column.Name}")}  DEFAULT {defaultValue} FOR {GetQuotedString(column.Name)}");
        }

        public Script DropDefaultValueConstraint(TableDefaultValueConstraint defaultValueConstraint)
        {
            return new DropDbObjectScript<TableDefaultValueConstraint>(GetDropConstraintSql(defaultValueConstraint));
        }

        public override Script SetIdentityEnabled(TableColumn column, bool enabled)
        {
            return new AlterDbObjectScript<Table>(
                $"SET IDENTITY_INSERT {GetQuotedFullTableName(column)} {(enabled ? "OFF" : "ON")}");
        }

        #endregion

        #region Database Operation

        public override Script CreateSchema(DatabaseSchema schema)
        {
            var script = $"CREATE SCHEMA {GetQuotedString(schema.Name)};";

            return new CreateDbObjectScript<DatabaseSchema>(script);
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

            var hasBigDataType = columns.Any(item => IsBigDataType(item));

            var option = GetCreateTableOption();

            #region Create Table

            var existsClause = $"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='{table.Name}')";

            var tableScript =
                $@"
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON

{(dbInterpreter.NotCreateIfExists ? existsClause : "")}
CREATE TABLE {quotedTableName}(
{string.Join("," + Environment.NewLine, columns.Select(item => dbInterpreter.ParseColumn(table, item)))}
) {option}{(hasBigDataType ? " TEXTIMAGE_ON [PRIMARY]" : "")}" + ";";

            sb.AppendLine(new CreateDbObjectScript<Table>(tableScript));

            #endregion

            #region Comment

            if (this.option.TableScriptsGenerateOption.GenerateComment)
            {
                if (!string.IsNullOrEmpty(table.Comment)) sb.AppendLine(SetTableComment(table));

                foreach (var column in columns.Where(item => !string.IsNullOrEmpty(item.Comment)))
                    sb.AppendLine(SetTableColumnComment(table, column));
            }

            #endregion

            #region Default Value

            if (this.option.TableScriptsGenerateOption.GenerateDefaultValue)
            {
                var defaultValueColumns = columns.Where(item =>
                    item.Schema == table.Schema && item.TableName == tableName &&
                    !string.IsNullOrEmpty(item.DefaultValue));

                foreach (var column in defaultValueColumns)
                {
                    if (ValueHelper.IsSequenceNextVal(column.DefaultValue))
                        continue;
                    if (column.DefaultValue.ToUpper().TrimStart().StartsWith("CREATE DEFAULT")) continue;

                    sb.AppendLine(AddDefaultValueConstraint(column));
                }
            }

            #endregion

            #region Primary Key

            if (this.option.TableScriptsGenerateOption.GeneratePrimaryKey && primaryKey != null)
            {
                sb.AppendLine(AddPrimaryKey(primaryKey));

                if (!string.IsNullOrEmpty(primaryKey.Comment)) sb.AppendLine(SetTableChildComment(primaryKey, true));
            }

            #endregion

            #region Foreign Key

            if (this.option.TableScriptsGenerateOption.GenerateForeignKey && foreignKeys != null)
                foreach (var foreignKey in foreignKeys)
                {
                    sb.AppendLine(AddForeignKey(foreignKey));

                    if (!string.IsNullOrEmpty(foreignKey.Comment))
                        sb.AppendLine(SetTableChildComment(foreignKey, true));
                }

            #endregion

            #region Index

            if (this.option.TableScriptsGenerateOption.GenerateIndex && indexes != null)
                foreach (var index in indexes)
                {
                    sb.AppendLine(AddIndex(index));

                    if (!string.IsNullOrEmpty(index.Comment)) sb.AppendLine(SetTableChildComment(index, true));
                }

            #endregion

            #region Constraint

            if (this.option.TableScriptsGenerateOption.GenerateConstraint && constraints != null)
                foreach (var constraint in constraints)
                {
                    sb.AppendLine(AddCheckConstraint(constraint));

                    if (!string.IsNullOrEmpty(constraint.Comment))
                        sb.AppendLine(SetTableChildComment(constraint, true));
                }

            #endregion

            sb.Append(new SpliterScript(scriptsDelimiter));

            return sb;
        }

        public override Script CreateUserDefinedType(UserDefinedType userDefinedType)
        {
            //only fetch first one, because SQLServer UDT is single attribute
            var attribute = userDefinedType.Attributes.First();

            var column = new TableColumn
            {
                DataType = attribute.DataType, MaxLength = attribute.MaxLength, Precision = attribute.Precision,
                Scale = attribute.Scale
            };
            var dataLength = dbInterpreter.GetColumnDataLength(column);

            var script =
                $@"CREATE TYPE {GetQuotedDbObjectNameWithSchema(userDefinedType)} FROM {GetQuotedString(attribute.DataType)}{(dataLength == "" ? "" : "(" + dataLength + ")")} {(attribute.IsRequired ? "NOT NULL" : "NULL")};";

            return new CreateDbObjectScript<UserDefinedType>(script);
        }

        public override Script CreateSequence(Sequence sequence)
        {
            var script =
                $@"CREATE SEQUENCE {GetQuotedDbObjectNameWithSchema(sequence)} AS {sequence.DataType} 
START WITH {sequence.StartValue}
INCREMENT BY {sequence.Increment}
MINVALUE {(long)sequence.MinValue}
MAXVALUE {(long)sequence.MaxValue}
{(sequence.Cycled ? "CYCLE" : "")}
{(sequence.UseCache ? "CACHE" : "")}{(sequence.CacheSize > 0 ? $" {sequence.CacheSize}" : "")};";

            return new CreateDbObjectScript<Sequence>(script);
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
            return $"DROP {typeName.ToUpper()} IF EXISTS {GetQuotedDbObjectNameWithSchema(dbObject)};";
        }

        public override IEnumerable<Script> SetConstrainsEnabled(bool enabled)
        {
            var procName = "sp_MSForEachTable";

            var sql =
                $@"
IF ServerProperty('Edition') != '{SqlServerInterpreter.AzureSQLFlag}'
BEGIN
  EXEC {procName} 'ALTER TABLE ? {(enabled ? "CHECK" : "NOCHECK")} CONSTRAINT ALL';
  EXEC {procName} 'ALTER TABLE ? {(enabled ? "ENABLE" : "DISABLE")} TRIGGER ALL';
END
ELSE 
BEGIN
    DECLARE @owner NVARCHAR(50)
	DECLARE @tableName NVARCHAR(256)

	DECLARE table_cursor CURSOR  
    FOR SELECT SCHEMA_NAME(schema_id),name FROM sys.tables  
	OPEN table_cursor  

    FETCH NEXT FROM table_cursor INTO @owner,@tableName
  
    WHILE @@FETCH_STATUS = 0  
    BEGIN  
        EXEC('ALTER TABLE ['+ @owner + '].[' + @tableName +'] {(enabled ? "CHECK" : "NOCHECK")} CONSTRAINT ALL');
        EXEC('ALTER TABLE ['+ @owner + '].[' + @tableName +'] {(enabled ? "ENABLE" : "DISABLE")} TRIGGER ALL');

        FETCH NEXT FROM table_cursor INTO @owner,@tableName  
    END  
  
    CLOSE table_cursor  
    DEALLOCATE table_cursor   
END";

            yield return new ExecuteProcedureScript(sql);
        }

        #endregion
    }
}