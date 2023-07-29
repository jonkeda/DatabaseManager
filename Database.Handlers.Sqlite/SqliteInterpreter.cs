using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.Interpreter.Builder;

namespace DatabaseInterpreter.Core
{
    public class SqliteInterpreter : DbInterpreter
    {
        #region Constructor

        public SqliteInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option) : base(connectionInfo,
            option)
        {
            dbConnector = GetDbConnector();
        }

        #endregion

        #region BulkCopy

        public override Task BulkCopyAsync(DbConnection connection, DataTable dataTable, BulkCopyInfo bulkCopyInfo)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Sql Query Clause

        protected override string GetSqlForPagination(string tableName, string columnNames, string orderColumns,
            string whereClause, long pageNumber, int pageSize)
        {
            var startEndRowNumber = PaginationHelper.GetStartEndRowNumber(pageNumber, pageSize);

            var orderByColumns = !string.IsNullOrEmpty(orderColumns) ? orderColumns : GetDefaultOrder();

            var orderBy = !string.IsNullOrEmpty(orderByColumns) ? $" ORDER BY {orderByColumns}" : "";

            var sb = new SqlBuilder();

            sb.Append($@"SELECT {columnNames}
							  FROM {tableName}
                             {whereClause} 
                             {orderBy}
                             LIMIT {pageSize} OFFSET {startEndRowNumber.StartRowNumber - 1}");

            return sb.Content;
        }

        #endregion

        #region Field & Property

        public override string CommentString => "--";
        public override string CommandParameterChar => "@";
        public override string UnicodeLeadingFlag => "";
        public override bool SupportQuotationChar => false;
        public override DatabaseType DatabaseType => DatabaseType.Sqlite;
        public override IndexType IndexType => IndexType.Primary | IndexType.Normal | IndexType.Unique;
        public override DatabaseObjectType SupportDbObjectType => DatabaseObjectType.Table | DatabaseObjectType.View;
        public override string DefaultDataType => "TEXT";
        public override string DefaultSchema => ConnectionInfo.Database;
        public override bool SupportBulkCopy => false;
        public override bool SupportNchar => false;
        public override string STR_CONCAT_CHARS => "||";

        #endregion

        #region Schema Information

        #region Database & Schema

        public override async Task<List<Database>> GetDatabasesAsync()
        {
            var databases = new List<Database> { new Database { Name = ConnectionInfo.Database } };

            return await Task.Run(() => { return databases; });
        }

        public override async Task<List<DatabaseSchema>> GetDatabaseSchemasAsync()
        {
            var database = ConnectionInfo.Database;

            var databaseSchemas = new List<DatabaseSchema>
                { new DatabaseSchema { Schema = database, Name = database } };

            return await Task.Run(() => { return databaseSchemas; });
        }

        public override Task<List<DatabaseSchema>> GetDatabaseSchemasAsync(DbConnection dbConnection)
        {
            return GetDatabaseSchemasAsync();
        }

        #endregion

        #region Table

        public override Task<List<Table>> GetTablesAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Table>(GetSqlForTables(filter));
        }

        public override Task<List<Table>> GetTablesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Table>(dbConnection, GetSqlForTables(filter));
        }

        private string GetSqlForTables(SchemaInfoFilter filter = null)
        {
            return GetSqlForTableViews(DatabaseObjectType.Table, filter);
        }

        private string GetSqlForTableViews(DatabaseObjectType dbObjectType, SchemaInfoFilter filter = null,
            bool includeDefinition = false)
        {
            var sb = new SqlBuilder();

            var type = dbObjectType.ToString().ToLower();
            string[] objectNames = null;
            var isScriptDbObject = false;
            var isSimpleMode = IsObjectFectchSimpleMode();

            if (dbObjectType == DatabaseObjectType.Table)
            {
                objectNames = filter?.TableNames;
            }
            else if (dbObjectType == DatabaseObjectType.View)
            {
                objectNames = filter?.ViewNames;
                isScriptDbObject = true;
            }

            var definition = (isScriptDbObject && !isSimpleMode) || includeDefinition ? ",sql as Definition" : "";

            sb.Append($@"SELECT name AS Name{definition}                         
                        FROM sqlite_schema WHERE type= '{type}'");

            sb.Append(GetFilterNamesCondition(filter, objectNames, "name"));

            sb.Append("ORDER BY name");

            return sb.Content;
        }

        #endregion

        #region View

        public override Task<List<View>> GetViewsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<View>(GetSqlForViews(filter));
        }

        public override Task<List<View>> GetViewsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<View>(dbConnection, GetSqlForViews(filter));
        }

        private string GetSqlForViews(SchemaInfoFilter filter = null)
        {
            return GetSqlForTableViews(DatabaseObjectType.View, filter);
        }

        #endregion

        #region Trigger

        public override Task<List<TableTrigger>> GetTableTriggersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableTrigger>(GetSqlForTriggers(filter));
        }

        public override Task<List<TableTrigger>> GetTableTriggersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableTrigger>(dbConnection, GetSqlForTriggers(filter));
        }

        private string GetSqlForTriggers(SchemaInfoFilter filter = null)
        {
            return GetSqlForTableChildren(DatabaseObjectType.Trigger, filter);
        }

        private string GetSqlForTableChildren(DatabaseObjectType dbObjectType, SchemaInfoFilter filter = null)
        {
            var sb = new SqlBuilder();

            var type = dbObjectType.ToString().ToLower();
            var isScriptDbObject = false;
            var isSimpleMode = IsObjectFectchSimpleMode();

            var tableNames = filter?.TableNames;
            string[] childrenNames = null;


            if (dbObjectType == DatabaseObjectType.Trigger)
            {
                isScriptDbObject = true;
                childrenNames = filter?.TableTriggerNames;
            }
            else if (dbObjectType == DatabaseObjectType.Column)
            {
                childrenNames = filter?.TableTriggerNames;
            }

            var definition = isScriptDbObject && !isSimpleMode ? ",sql as Definition" : "";

            var unique = dbObjectType == DatabaseObjectType.Index
                ? ",CASE WHEN INSTR(sql, 'UNIQUE')>0 THEN 1 ELSE 0 END AS IsUnique"
                : "";

            sb.Append($@"SELECT name AS Name,tbl_name AS TableName{definition}{unique}                         
                        FROM sqlite_schema WHERE type= '{type}'");

            sb.Append(GetFilterNamesCondition(filter, tableNames, "tbl_name"));
            sb.Append(GetFilterNamesCondition(filter, childrenNames, "name"));

            if (dbObjectType == DatabaseObjectType.Index) sb.Append("AND name not like 'sqlite_autoindex%'");

            sb.Append("ORDER BY tbl_name,name");

            return sb.Content;
        }

        #endregion

        #region Column

        public override Task<List<TableColumn>> GetTableColumnsAsync(SchemaInfoFilter filter = null)
        {
            return GetTableColumnsAsync(CreateConnection(), filter);
        }

        public override async Task<List<TableColumn>> GetTableColumnsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            if (filter?.TableNames == null)
            {
                if (filter == null) filter = new SchemaInfoFilter();

                filter.TableNames = await GetTableNamesAsync();
            }

            var columns = await GetDbObjectsAsync<TableColumn>(dbConnection, GetSqlForTableColumns(filter));

            return columns;
        }

        private string GetSqlForTableColumns(SchemaInfoFilter filter = null)
        {
            var sb = new SqlBuilder();

            var tableNames = filter?.TableNames;

            if (tableNames != null)
                for (var i = 0; i < tableNames.Length; i++)
                {
                    var tableName = tableNames[i];

                    if (i > 0) sb.Append("UNION ALL");

                    sb.Append($@"SELECT name AS Name,'{tableName}' AS TableName,
                                TRIM(REPLACE(type,'AUTO_INCREMENT','')) AS DataType,
                                CASE WHEN INSTR(UPPER(type),'NUMERIC')>=1 AND INSTR(type,'(')>0 THEN CAST(TRIM(SUBSTR(type,INSTR(type,'(')+1,IIF(INSTR(type,',')==0, INSTR(type,')'),INSTR(type,','))-INSTR(type,'(')-1)) AS INTEGER) ELSE NULL END AS Precision,
                                CASE WHEN INSTR(UPPER(type),'NUMERIC')>=1 AND INSTR(type,',')>0 THEN CAST(TRIM(SUBSTR(type,INSTR(type,',')+1,INSTR(type,')')-INSTR(type,',')-1)) AS INTEGER) ELSE NULL END AS Scale,
                                CASE WHEN INSTR(type,'AUTO_INCREMENT')>0 THEN 1 ELSE 0 END AS IsIdentity,
                                CASE WHEN ""notnull""=1 THEN 0 ELSE 1 END AS IsNullable,
                                dflt_value AS DefaultValue, pk AS IsPrimaryKey, cid AS ""Order""
                                FROM PRAGMA_TABLE_INFO('{tableName}')");
                }

            return sb.Content;
        }

        #endregion

        #region Primary Key

        public override Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(SchemaInfoFilter filter = null)
        {
            return GetTablePrimaryKeyItemsAsync(CreateConnection(), filter);
        }

        public override async Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            if (filter?.TableNames == null)
            {
                if (filter == null) filter = new SchemaInfoFilter();

                filter.TableNames = await GetTableNamesAsync();
            }

            var primaryKeyItems =
                await GetDbObjectsAsync<TablePrimaryKeyItem>(dbConnection, GetSqlForPrimaryKeys(filter));

            await MakeupTableChildrenNames(primaryKeyItems, filter);

            return primaryKeyItems;
        }

        private string GetSqlForPrimaryKeys(SchemaInfoFilter filter = null)
        {
            var sb = new SqlBuilder();

            var tableNames = filter?.TableNames;

            if (tableNames != null)
                for (var i = 0; i < tableNames.Length; i++)
                {
                    var tableName = tableNames[i];

                    if (i > 0) sb.Append("UNION ALL");

                    sb.Append($@"SELECT '{tableName}' as TableName, name AS ColumnName
                        FROM PRAGMA_TABLE_INFO('{tableName}')
                        WHERE pk>0");
                }

            return sb.Content;
        }

        private async Task MakeupTableChildrenNames(IEnumerable<TableColumnChild> columnChildren,
            SchemaInfoFilter filter = null)
        {
            if (columnChildren == null || !columnChildren.Any()) return;

            var tablesSql = GetSqlForTableViews(DatabaseObjectType.Table, filter, true);

            var tables = await GetDbObjectsAsync<Table>(tablesSql);

            foreach (var table in tables)
            {
                var tableName = table.Name;
                var definition = table.Definition;

                List<List<string>> columnDetails = null;

                var name = ExtractNameFromTableDefinition(definition,
                    DbObjectHelper.GetDatabaseObjectType(columnChildren.FirstOrDefault()), out columnDetails);

                if (!string.IsNullOrEmpty(name))
                {
                    var children = columnChildren.Where(item => item.TableName == tableName);

                    foreach (var child in children) child.Name = name;
                }
            }
        }

        private async Task<List<DatabaseObject>> GetTableChildren(DatabaseObjectType dbObjectType,
            DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            var databaseObjects = new List<DatabaseObject>();

            if (dbConnection == null) dbConnection = CreateConnection();

            var tablesSql = GetSqlForTableViews(DatabaseObjectType.Table, filter, true);

            var tables = await GetDbObjectsAsync<Table>(dbConnection, tablesSql);

            var flag = "";

            if (dbObjectType == DatabaseObjectType.Index) //unique
                flag = "UNIQUE";
            else if (dbObjectType == DatabaseObjectType.Constraint) //check constraint
                flag = "CHECK";

            foreach (var table in tables)
            {
                var tableName = table.Name;
                var definition = table.Definition;

                var columns = GetTableColumnDetails(definition);

                var matchedColumns = columns.Where(item => item.Any(t => t.Trim().ToUpper().StartsWith(flag)));

                foreach (var columnItems in matchedColumns)
                {
                    var isTableConstraint = columnItems.First().ToUpper().Trim() == "CONSTRAINT";

                    string objName = null;
                    var columNames = new List<string>();

                    var index = FindIndexInList(columnItems, flag);

                    var name = ExtractNameFromColumnDefintion(columnItems, dbObjectType);

                    if (!string.IsNullOrEmpty(name)) objName = name;

                    if (isTableConstraint)
                    {
                        if (dbObjectType == DatabaseObjectType.Index)
                            columNames = ExtractColumnNamesFromTableConstraint(columnItems, index);
                    }
                    else
                    {
                        columNames.Add(columnItems.First());
                    }

                    if (dbObjectType == DatabaseObjectType.Index) //unique
                    {
                        for (var i = 0; i < columNames.Count; i++)
                        {
                            var tableIndexItem = new TableIndexItem
                            {
                                Name = objName,
                                ColumnName = columNames[i],
                                TableName = table.Name,
                                Type = "Unique",
                                IsUnique = true,
                                Order = i + 1
                            };

                            databaseObjects.Add(tableIndexItem);
                        }
                    }
                    else if (dbObjectType == DatabaseObjectType.Constraint) //check constraint
                    {
                        var columnName = columNames.FirstOrDefault();

                        var tableConstraint = new TableConstraint
                        {
                            Name = objName,
                            ColumnName = columnName,
                            TableName = table.Name,
                            Definition = ExtractDefinitionFromTableConstraint(columnItems, index)
                        };

                        databaseObjects.Add(tableConstraint);
                    }
                }
            }

            return databaseObjects;
        }

        #endregion

        #region Foreign Key

        public override Task<List<TableForeignKeyItem>> GetTableForeignKeyItemsAsync(SchemaInfoFilter filter = null,
            bool isFilterForReferenced = false)
        {
            return GetTableForeignKeyItemsAsync(CreateConnection(), filter, isFilterForReferenced);
        }

        public override async Task<List<TableForeignKeyItem>> GetTableForeignKeyItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            if (filter?.TableNames == null)
            {
                if (filter == null) filter = new SchemaInfoFilter();

                filter = new SchemaInfoFilter { TableNames = await GetTableNamesAsync() };
            }

            var foreignKeyItems = await GetDbObjectsAsync<TableForeignKeyItem>(dbConnection,
                GetSqlForForeignKeys(filter, isFilterForReferenced));

            await MakeupTableChildrenNames(foreignKeyItems, filter);

            return foreignKeyItems;
        }

        private async Task<string[]> GetTableNamesAsync()
        {
            var tables = await GetTablesAsync();

            return tables.Select(item => item.Name).ToArray();
        }

        private string GetSqlForForeignKeys(SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            var sb = new SqlBuilder();

            if (!isFilterForReferenced)
            {
                var tableNames = filter?.TableNames;

                if (tableNames != null)
                    for (var i = 0; i < tableNames.Length; i++)
                    {
                        var tableName = tableNames[i];

                        if (i > 0) sb.Append("UNION ALL");

                        sb.Append($@"SELECT ""table"" AS ReferencedTableName, ""to"" AS ReferencedColumnName,
                                '{tableName}' AS TableName, ""from"" AS ColumnName,
                                CASE WHEN on_update='CASCADE' THEN 1 ELSE 0 END AS UpdateCascade,
                                CASE WHEN on_delete='CASCADE' THEN 1 ELSE 0 END AS DeleteCascade
                                FROM PRAGMA_foreign_key_list('{tableName}')");
                    }
            }

            return sb.Content;
        }

        #endregion

        #region Index

        public override Task<List<TableIndexItem>> GetTableIndexItemsAsync(SchemaInfoFilter filter = null,
            bool includePrimaryKey = false)
        {
            return GetTableIndexItemsAsync(CreateConnection(), filter);
        }

        public override async Task<List<TableIndexItem>> GetTableIndexItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            var items = await GetDbObjectsAsync<TableIndexItem>(dbConnection, GetSqlForIndexes(filter));

            items.ForEach(item =>
            {
                if (item.Type == null)
                {
                    if (item.IsUnique)
                        item.Type = "Unique";
                    else
                        item.Type = "Normal";
                }
            });

            var columns = await GetDbObjectsAsync<TableIndexItem>(GetSqlForIndexColumns(items));

            foreach (var item1 in items)
            {
                var column = columns.FirstOrDefault(item => item1.Name == item.Name);

                if (column != null) item1.ColumnName = column.ColumnName;
            }

            var uniqueIndexes = await GetTableChildren(DatabaseObjectType.Index, null, filter);

            var uniqueItems = new List<TableIndexItem>();

            foreach (var unique in uniqueIndexes)
            {
                if (!string.IsNullOrEmpty(unique.Name) && items.Any(item =>
                        item.TableName == (unique as TableIndexItem).TableName && item.Name == unique.Name)) continue;

                uniqueItems.Add(unique as TableIndexItem);
            }

            items.AddRange(uniqueItems);

            return items;
        }

        private string GetSqlForIndexes(SchemaInfoFilter filter = null)
        {
            return GetSqlForTableChildren(DatabaseObjectType.Index, filter);
        }

        private string GetSqlForIndexColumns(List<TableIndexItem> indexes)
        {
            var sb = new SqlBuilder();

            var i = 0;

            foreach (var item in indexes)
            {
                if (i > 0) sb.Append("UNION ALL");

                sb.Append($"SELECT '{item.Name}' AS Name, name AS ColumnName FROM PRAGMA_INDEX_INFO('{item.Name}')");

                i++;
            }

            return sb.Content;
        }

        #endregion

        #region Constraint

        public override Task<List<TableConstraint>> GetTableConstraintsAsync(SchemaInfoFilter filter = null)
        {
            return GetTableConstraintsAsync(CreateConnection(), filter);
        }

        public override async Task<List<TableConstraint>> GetTableConstraintsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            var constraints = new List<TableConstraint>();

            var dbObjects = await GetTableChildren(DatabaseObjectType.Constraint, dbConnection, filter);

            foreach (var dbObject in dbObjects) constraints.Add(dbObject as TableConstraint);

            return constraints;
        }

        #endregion

        #region Sequence

        public override Task<List<Sequence>> GetSequencesAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Sequence>("");
        }

        public override Task<List<Sequence>> GetSequencesAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Sequence>(dbConnection, "");
        }

        #endregion

        #region User Defined Type

        public override Task<List<UserDefinedTypeAttribute>> GetUserDefinedTypeAttributesAsync(
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<UserDefinedTypeAttribute>("");
        }

        public override Task<List<UserDefinedTypeAttribute>> GetUserDefinedTypeAttributesAsync(
            DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<UserDefinedTypeAttribute>(dbConnection, "");
        }

        #endregion

        #region Function

        public override Task<List<Function>> GetFunctionsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Function>("");
        }

        public override Task<List<Function>> GetFunctionsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Function>(dbConnection, "");
        }

        #endregion

        #region Procedure

        public override Task<List<Procedure>> GetProceduresAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Procedure>("");
        }

        public override Task<List<Procedure>> GetProceduresAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Procedure>(dbConnection, "");
        }

        #endregion

        #region Routine Parameter

        public override Task<List<RoutineParameter>> GetFunctionParametersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>("");
        }

        public override Task<List<RoutineParameter>> GetFunctionParametersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(dbConnection, "");
        }

        public override Task<List<RoutineParameter>> GetProcedureParametersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>("");
        }

        public override Task<List<RoutineParameter>> GetProcedureParametersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(dbConnection, "");
        }

        #endregion

        #endregion

        #region Dependency

        #region View->Column Usage

        public override Task<List<ViewColumnUsage>> GetViewColumnUsages(SchemaInfoFilter filter)
        {
            return GetDbObjectUsagesAsync<ViewColumnUsage>("");
        }

        public override Task<List<ViewColumnUsage>> GetViewColumnUsages(DbConnection dbConnection,
            SchemaInfoFilter filter)
        {
            return GetDbObjectUsagesAsync<ViewColumnUsage>(dbConnection, "");
        }

        #endregion

        #region View->Table Usage

        public override Task<List<ViewTableUsage>> GetViewTableUsages(SchemaInfoFilter filter,
            bool isFilterForReferenced = false)
        {
            return GetDbObjectUsagesAsync<ViewTableUsage>("");
        }

        public override Task<List<ViewTableUsage>> GetViewTableUsages(DbConnection dbConnection,
            SchemaInfoFilter filter, bool isFilterForReferenced = false)
        {
            return GetDbObjectUsagesAsync<ViewTableUsage>(dbConnection, "");
        }

        #endregion

        #region Routine Script Usage

        public override Task<List<RoutineScriptUsage>> GetRoutineScriptUsages(SchemaInfoFilter filter,
            bool isFilterForReferenced = false, bool includeViewTableUsages = false)
        {
            return GetDbObjectUsagesAsync<RoutineScriptUsage>("");
        }

        public override Task<List<RoutineScriptUsage>> GetRoutineScriptUsages(DbConnection dbConnection,
            SchemaInfoFilter filter, bool isFilterForReferenced = false, bool includeViewTableUsages = false)
        {
            return GetDbObjectUsagesAsync<RoutineScriptUsage>(dbConnection, "");
        }

        #endregion

        #endregion

        #region Common Method

        public override bool IsLowDbVersion(string serverVersion)
        {
            return IsLowDbVersion(serverVersion, "3");
        }

        public override DbConnector GetDbConnector()
        {
            return new DbConnector(new SqliteProvider(), new SqliteConnectionStringBuilder(), ConnectionInfo);
        }

        private List<List<string>> GetTableColumnDetails(string definition)
        {
            var columnDetails = new List<List<string>>();

            var firstIndex = definition.IndexOf("(", StringComparison.Ordinal);
            var lastIndex = definition.LastIndexOf(")", definition.Length - 1);

            var innerContent = definition.Substring(firstIndex + 1, lastIndex - firstIndex - 1);

            var singleQuotationCharCount = 0;
            var leftParenthesisesCount = 0;
            var rightParenthesisesCount = 0;

            var sb = new StringBuilder();

            #region Extract Columns

            var columns = new List<string>();

            for (var i = 0; i < innerContent.Length; i++)
            {
                var c = innerContent[i];

                if (c == '\'')
                    singleQuotationCharCount++;
                else if (c == '(')
                    leftParenthesisesCount++;
                else if (c == ')')
                    rightParenthesisesCount++;
                else if (c == ',')
                    if (singleQuotationCharCount % 2 == 0 && leftParenthesisesCount == rightParenthesisesCount)
                    {
                        columns.Add(sb.ToString());
                        sb.Clear();
                        continue;
                    }

                sb.Append(c);
            }

            if (sb.Length > 0) columns.Add(sb.ToString());

            #endregion

            sb.Clear();

            #region Parse Columns

            foreach (var column in columns)
            {
                singleQuotationCharCount = 0;
                leftParenthesisesCount = 0;
                rightParenthesisesCount = 0;

                var columnItems = new List<string>();

                foreach (var c in column)
                {
                    if (c == '\'')
                        singleQuotationCharCount++;
                    else if (c == '(')
                        leftParenthesisesCount++;
                    else if (c == ')')
                        rightParenthesisesCount++;
                    else if (c == ' ')
                        if (singleQuotationCharCount % 2 == 0 && leftParenthesisesCount == rightParenthesisesCount)
                        {
                            var item = sb.ToString().Trim();

                            if (item.Length > 0) columnItems.Add(item);

                            sb.Clear();
                            continue;
                        }

                    sb.Append(c);
                }

                if (sb.Length > 0)
                {
                    columnItems.Add(sb.ToString().Trim());
                    sb.Clear();
                }

                columnDetails.Add(columnItems);
            }

            #endregion

            sb.Clear();

            return columnDetails;
        }

        private string ExtractNameFromTableDefinition(string definition, DatabaseObjectType dbObjectType,
            out List<List<string>> columnDetails)
        {
            columnDetails = GetTableColumnDetails(definition);

            foreach (var item in columnDetails)
            {
                var name = ExtractNameFromColumnDefintion(item, dbObjectType);

                if (!string.IsNullOrEmpty(name)) return name;
            }

            return string.Empty;
        }

        private string ExtractNameFromColumnDefintion(List<string> columnItems, DatabaseObjectType dbObjectType)
        {
            var indexes = FindAllIndexesInList(columnItems, "CONSTRAINT");

            if (indexes.Count() > 0)
            {
                var matched = false;
                var index = -1;

                if (dbObjectType == DatabaseObjectType.PrimaryKey &&
                    (index = FindIndexInList(columnItems, "PRIMARY")) > 0)
                    matched = true;
                else if (dbObjectType == DatabaseObjectType.ForeignKey &&
                         (index = FindIndexInList(columnItems, "REFERENCES")) > 0)
                    matched = true;
                else if (dbObjectType == DatabaseObjectType.Constraint &&
                         (index = FindIndexInList(columnItems, "CHECK")) > 0)
                    matched = true;
                else if (dbObjectType == DatabaseObjectType.Index &&
                         (index = FindIndexInList(columnItems, "UNIQUE")) > 0) matched = true;

                if (matched)
                {
                    var closestConstraintIndex = indexes.Where(item => item < index).Max();

                    return ExtractTableChildName(columnItems, closestConstraintIndex + 1);
                }
            }

            return string.Empty;
        }

        private List<string> ExtractColumnNamesFromTableConstraint(List<string> columnItems, int startIndex)
        {
            var columNames = new List<string>();

            var item = columnItems[startIndex];

            if (item.Contains("("))
            {
                var index = item.IndexOf("(", StringComparison.Ordinal);

                columNames = item.Substring(index + 1).Trim().TrimEnd(')').Split(',').Select(item1 => item1.Trim())
                    .ToList();
            }
            else
            {
                item = columnItems.Skip(startIndex + 1).FirstOrDefault(item1 => item1.Trim().StartsWith("("));

                columNames = item.Trim().Trim('(', ')').Split(',').Select(item1 => item1.Trim()).ToList();
            }

            return columNames;
        }

        private string ExtractDefinitionFromTableConstraint(List<string> columnItems, int startIndex)
        {
            var item = columnItems[startIndex];

            if (item.Contains("("))
            {
                var index = item.IndexOf("(", StringComparison.Ordinal);

                return item.Substring(index + 1);
            }

            return columnItems.Skip(startIndex + 1).FirstOrDefault(item1 => item1.Trim().Length > 0);
        }

        private int FindIndexInList(List<string> list, string value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i].Trim().ToUpper();

                if (item == value || item.StartsWith($"{value}(")) return i;
            }

            return -1;
        }

        private IEnumerable<int> FindAllIndexesInList(List<string> list, string value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i].Trim().ToUpper();

                if (item == value || item.StartsWith($"{value}(")) yield return i;
            }
        }

        private string ExtractTableChildName(List<string> items, int startIndex)
        {
            var keywords = KeywordManager.GetKeywords(DatabaseType);

            return items.Skip(startIndex).FirstOrDefault(item => item.Length > 0 && !keywords.Contains(item));
        }

        #endregion

        #region Parse Column & DataType

        public override string ParseColumn(Table table, TableColumn column)
        {
            var dataType = ParseDataType(column);
            var requiredClause = column.IsRequired ? "NOT NULL" : "NULL";

            if (column.IsComputed)
                return
                    $"{GetQuotedString(column.Name)} {dataType} GENERATED ALWAYS AS ({column.ComputeExp}) STORED {requiredClause}";

            var identityClause = Option.TableScriptsGenerateOption.GenerateIdentity && column.IsIdentity
                ? "AUTO_INCREMENT"
                : "";
            var commentClause =
                !string.IsNullOrEmpty(column.Comment) && Option.TableScriptsGenerateOption.GenerateComment
                    ? $"COMMENT '{ReplaceSplitChar(ValueHelper.TransferSingleQuotation(column.Comment))}'"
                    : "";
            var defaultValueClause =
                Option.TableScriptsGenerateOption.GenerateDefaultValue && !string.IsNullOrEmpty(column.DefaultValue) &&
                !ValueHelper.IsSequenceNextVal(column.DefaultValue)
                    ? " DEFAULT " + StringHelper.GetParenthesisedString(GetColumnDefaultValue(column))
                    : "";
            var scriptComment = string.IsNullOrEmpty(column.ScriptComment) ? "" : $"/*{column.ScriptComment}*/";

            return
                $"{GetQuotedString(column.Name)} {dataType} {identityClause} {requiredClause} {defaultValueClause} {scriptComment}{commentClause}";
        }

        public override string ParseDataType(TableColumn column)
        {
            var dataLength = GetColumnDataLength(column);

            if (!string.IsNullOrEmpty(dataLength)) dataLength = $"({dataLength})";

            var dataType = $"{column.DataType} {dataLength}";

            return dataType.Trim();
        }

        public override string GetColumnDataLength(TableColumn column)
        {
            var dataType = column.DataType;
            var isChar = DataTypeHelper.IsCharType(dataType);
            var isBinary = DataTypeHelper.IsBinaryType(dataType);

            var dataTypeInfo = GetDataTypeInfo(dataType);

            var dataTypeSpec = GetDataTypeSpecification(dataTypeInfo.DataType);

            if (dataTypeSpec != null)
            {
                var args = dataTypeSpec.Args.ToLower().Trim();

                if (string.IsNullOrEmpty(args)) return string.Empty;

                if (args == "precision,scale")
                {
                    if (dataTypeInfo.Args != null && !dataTypeInfo.Args.Contains(","))
                        return $"{column.Precision ?? 0},{column.Scale ?? 0}";
                    if (column.Precision.HasValue && column.Scale.HasValue) return $"{column.Precision},{column.Scale}";
                }
            }

            return string.Empty;
        }

        #endregion
    }
}