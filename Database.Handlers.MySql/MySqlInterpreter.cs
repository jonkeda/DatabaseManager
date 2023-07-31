using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.Interpreter.Builder;
using Databases.Interpreter.Connection;
using Microsoft.SqlServer.Types;
using MySqlConnector;

namespace DatabaseInterpreter.Core
{
    public class MySqlInterpreter : DbInterpreter
    {
        #region Constructor

        public MySqlInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option) : base(connectionInfo,
            option)
        {
            dbConnector = GetDbConnector();
        }

        #endregion

        #region Field & Property

        public const int DEFAULT_PORT = 3306;
        public override string UnicodeLeadingFlag => "";
        public override string CommandParameterChar => "@";
        public const char QuotedLeftChar = '`';
        public const char QuotedRightChar = '`';
        public override bool SupportQuotationChar => true;
        public override char QuotationLeftChar => QuotedLeftChar;
        public override char QuotationRightChar => QuotedRightChar;
        public override string CommentString => "#";
        public override DatabaseType DatabaseType => DatabaseType.MySql;
        public override string DefaultDataType => "varchar";
        public static readonly DateTime Timestamp_Max_Value = DateTime.Parse("2038-01-19 03:14:07");
        public override string DefaultSchema => ConnectionInfo.Database;
        public override IndexType IndexType => IndexType.Primary | IndexType.Normal | IndexType.FullText;

        public override DatabaseObjectType SupportDbObjectType => DatabaseObjectType.Table | DatabaseObjectType.View |
                                                                  DatabaseObjectType.Function |
                                                                  DatabaseObjectType.Procedure;

        public override bool SupportBulkCopy => true;
        public override bool SupportNchar => false;

        public override List<string> BuiltinDatabases => new List<string>
            { "sys", "mysql", "information_schema", "performance_schema" };

        public const int NameMaxLength = 64;
        public const int KeyIndexColumnMaxLength = 500;
        public readonly string DbCharset = Setting.MySqlCharset;
        public readonly string DbCharsetCollation = Setting.MySqlCharsetCollation;
        public string NotCreateIfExistsClause => NotCreateIfExists ? "IF NOT EXISTS" : "";

        #endregion

        #region Common Method

        public override DbConnector GetDbConnector()
        {
            return new DbConnector(new MySqlProvider(), new MySqlConnectionBuilder(), ConnectionInfo);
        }

        public override bool IsLowDbVersion(string version)
        {
            return IsLowDbVersion(version, "8");
        }

        #endregion

        #region Schema Information

        #region Database

        public override Task<List<Database>> GetDatabasesAsync()
        {
            var sql =
                $"SELECT SCHEMA_NAME AS `Name` FROM INFORMATION_SCHEMA.`SCHEMATA` {GetExcludeBuiltinDbNamesCondition("SCHEMA_NAME")} ORDER BY SCHEMA_NAME";

            return GetDbObjectsAsync<Database>(sql);
        }

        public string GetDatabaseVersion()
        {
            return GetDatabaseVersion(dbConnector.CreateConnection());
        }

        public string GetDatabaseVersion(DbConnection dbConnection)
        {
            var sql = "select version() as version";

            return dbConnection.QuerySingleOrDefault(sql).version;
        }

        #endregion

        #region Database Schema

        public override async Task<List<DatabaseSchema>> GetDatabaseSchemasAsync()
        {
            var database = ConnectionInfo.Database;

            var databaseSchemas = new List<DatabaseSchema>
                { new DatabaseSchema { Schema = database, Name = database } };

            return await Task.Run(() => { return databaseSchemas; });
        }

        public override async Task<List<DatabaseSchema>> GetDatabaseSchemasAsync(DbConnection dbConnection)
        {
            return await GetDatabaseSchemasAsync();
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

        #region Function

        public override Task<List<Function>> GetFunctionsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Function>(GetSqlForRoutines(DatabaseObjectType.Function, filter));
        }

        public override Task<List<Function>> GetFunctionsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Function>(dbConnection, GetSqlForRoutines(DatabaseObjectType.Function, filter));
        }

        private string GetSqlForRoutines(DatabaseObjectType databaseObjectType, SchemaInfoFilter filter = null)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();

            var isFunction = databaseObjectType == DatabaseObjectType.Function;
            var type = isFunction ? "FUNCTION" : "PROCEDURE";
            var objectNames = isFunction ? filter?.FunctionNames : filter?.ProcedureNames;

            var sb = CreateSqlBuilder();

            if (isSimpleMode)
            {
                sb.Append($@"SELECT ROUTINE_NAME AS `Name`, ROUTINE_SCHEMA AS `Schema`                        
                        FROM INFORMATION_SCHEMA.`ROUTINES`
                        WHERE ROUTINE_TYPE = '{type}' AND ROUTINE_SCHEMA = '{ConnectionInfo.Database}'");

                sb.Append(GetFilterNamesCondition(filter, objectNames, "ROUTINE_NAME"));

                sb.Append("ORDER BY ROUTINE_NAME");
            }
            else
            {
                var functionReturns = isFunction ? ", 'RETURNS ',IFNULL(r.DATA_TYPE,''), ' '" : "";
                var procParameterMode = isFunction ? "" : "IFNULL(p.PARAMETER_MODE,''),' ',";

                sb.Append($@"SELECT ROUTINE_SCHEMA AS `Schema`, ROUTINE_NAME AS `Name`,r.DATA_TYPE AS `DataType`,
                        CONVERT(CONCAT('CREATE {type}  `', ROUTINE_SCHEMA, '`.`', ROUTINE_NAME, '`(', 
                        IFNULL(GROUP_CONCAT(CONCAT(IFNULL(CASE p.PARAMETER_MODE WHEN 'IN' THEN '' ELSE p.PARAMETER_MODE END,''),' ',p.PARAMETER_NAME, ' ', p.`DTD_IDENTIFIER`)),''), 
                        ') '{functionReturns}, CHAR(10), ROUTINE_DEFINITION) USING utf8)  AS `Definition` 
                        FROM information_schema.Routines r
                        LEFT JOIN information_schema.`PARAMETERS` p ON r.`ROUTINE_SCHEMA`= p.`SPECIFIC_SCHEMA` AND r.`ROUTINE_NAME`= p.`SPECIFIC_NAME`
                        WHERE r.ROUTINE_TYPE = '{type}' AND ROUTINE_SCHEMA = '{ConnectionInfo.Database}'");

                sb.Append(GetFilterNamesCondition(filter, objectNames, "r.ROUTINE_NAME"));

                sb.Append(@"GROUP BY ROUTINE_SCHEMA,ROUTINE_NAME,r.DATA_TYPE
                          ORDER BY r.ROUTINE_NAME");
            }

            return sb.Content;
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
            var isSimpleMode = IsObjectFectchSimpleMode();

            var sb = CreateSqlBuilder();

            sb.Append(
                $@"SELECT TABLE_SCHEMA AS `Schema`, TABLE_NAME AS `Name` {(isSimpleMode ? "" : ", TABLE_COMMENT AS `Comment`, 1 AS `IdentitySeed`, 1 AS `IdentityIncrement`")}
                        FROM INFORMATION_SCHEMA.`TABLES`
                        WHERE TABLE_TYPE ='BASE TABLE' AND TABLE_SCHEMA ='{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, "TABLE_NAME"));
            sb.Append("ORDER BY TABLE_NAME");

            return sb.Content;
        }

        #endregion

        #region Table Column

        public override Task<List<TableColumn>> GetTableColumnsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableColumn>(GetSqlForTableColumns(filter));
        }

        public override Task<List<TableColumn>> GetTableColumnsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableColumn>(dbConnection, GetSqlForTableColumns(filter));
        }

        private bool IsSupportComputeColumn()
        {
            return !IsLowDbVersion(GetDbVersion(), "5.7");
        }

        private string GetSqlForTableColumns(SchemaInfoFilter filter = null)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();
            var isForView = IsForViewColumnFilter(filter);

            var supportComputeColumn = IsSupportComputeColumn();

            var computeExp = !supportComputeColumn
                ? "NULL"
                : $"REPLACE(REPLACE(REPLACE(C.GENERATION_EXPRESSION,'\\\\',''),(SELECT CONCAT('_',DEFAULT_CHARACTER_SET_NAME) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = \"INFORMATION_SCHEMA\"),''),\r\n                        (SELECT CONCAT('_',DEFAULT_CHARACTER_SET_NAME) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{ConnectionInfo.Database}'),'')";
            var detailColums = isForView || isSimpleMode
                ? ""
                : $@",COLUMN_DEFAULT AS `DefaultValue`,COLUMN_COMMENT AS `Comment`, {computeExp} AS `ComputeExp`";

            var joinTable = !isForView
                ? "JOIN INFORMATION_SCHEMA.`TABLES` AS T ON T.`TABLE_NAME`= C.`TABLE_NAME` AND T.TABLE_TYPE='BASE TABLE' AND T.TABLE_SCHEMA=C.TABLE_SCHEMA"
                : "JOIN INFORMATION_SCHEMA.`VIEWS` AS V ON V.`TABLE_NAME`= C.`TABLE_NAME` AND V.TABLE_SCHEMA=C.TABLE_SCHEMA";


            var sb = CreateSqlBuilder();

            sb.Append(
                $@"SELECT C.TABLE_SCHEMA AS `Schema`, C.TABLE_NAME AS `TableName`, COLUMN_NAME AS `Name`, COLUMN_TYPE AS `DataType`, 
                        CHARACTER_MAXIMUM_LENGTH AS `MaxLength`, CASE IS_NULLABLE WHEN 'YES' THEN 1 ELSE 0 END AS `IsNullable`,ORDINAL_POSITION AS `Order`,
                        NUMERIC_PRECISION AS `Precision`,NUMERIC_SCALE AS `Scale`,
                        CASE EXTRA WHEN 'auto_increment' THEN 1 ELSE 0 END AS `IsIdentity`,'' AS `DataTypeSchema`
                        {detailColums} 
                        FROM INFORMATION_SCHEMA.`COLUMNS` AS C
                        {joinTable}
                        WHERE C.TABLE_SCHEMA ='{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, "C.TABLE_NAME"));

            sb.Append("ORDER BY C.TABLE_NAME,C.ORDINAL_POSITION");

            return sb.Content;
        }

        #endregion

        #region Table Primary Key

        public override Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TablePrimaryKeyItem>(GetSqlForTablePrimaryKeyItems(filter));
        }

        public override Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TablePrimaryKeyItem>(dbConnection, GetSqlForTablePrimaryKeyItems(filter));
        }

        private string GetSqlForTablePrimaryKeyItems(SchemaInfoFilter filter = null)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();
            var commentColumn = isSimpleMode ? "" : ",S.INDEX_COMMENT AS `Comment`";
            var commentJoin = isSimpleMode
                ? ""
                : "LEFT JOIN INFORMATION_SCHEMA.STATISTICS AS S ON K.TABLE_SCHEMA=S.TABLE_SCHEMA AND K.TABLE_NAME=S.TABLE_NAME AND K.CONSTRAINT_NAME=S.INDEX_NAME AND K.ORDINAL_POSITION=S.SEQ_IN_INDEX";

            var sb = CreateSqlBuilder();

            //Note:TABLE_SCHEMA of INFORMATION_SCHEMA.KEY_COLUMN_USAGE will improve performance when it's used in where clause, just use CONSTRAINT_SCHEMA in join on clause because it equals to TABLE_SCHEMA.
            sb.Append(
                $@"SELECT C.`CONSTRAINT_SCHEMA` AS `Schema`, K.TABLE_NAME AS `TableName`, K.CONSTRAINT_NAME AS `Name`, 
                        K.COLUMN_NAME AS `ColumnName`, K.`ORDINAL_POSITION` AS `Order`, 0 AS `IsDesc`{commentColumn}
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA AND C.TABLE_NAME = K.TABLE_NAME AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                        {commentJoin}
                        WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND K.TABLE_SCHEMA ='{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, "C.TABLE_NAME"));

            return sb.Content;
        }

        #endregion

        #region Table Foreign Key

        public override Task<List<TableForeignKeyItem>> GetTableForeignKeyItemsAsync(SchemaInfoFilter filter = null,
            bool isFilterForReferenced = false)
        {
            return GetDbObjectsAsync<TableForeignKeyItem>(GetSqlForTableForeignKeyItems(filter, isFilterForReferenced));
        }

        public override Task<List<TableForeignKeyItem>> GetTableForeignKeyItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            return GetDbObjectsAsync<TableForeignKeyItem>(dbConnection,
                GetSqlForTableForeignKeyItems(filter, isFilterForReferenced));
        }

        private string GetSqlForTableForeignKeyItems(SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();
            var commentColumn = isSimpleMode ? "" : ",S.`INDEX_COMMENT` AS `Comment`";
            var commentJoin = isSimpleMode
                ? ""
                : "LEFT JOIN INFORMATION_SCHEMA.STATISTICS AS S ON K.TABLE_SCHEMA=S.TABLE_SCHEMA AND K.TABLE_NAME=S.TABLE_NAME AND K.CONSTRAINT_NAME=S.INDEX_NAME AND K.ORDINAL_POSITION=S.SEQ_IN_INDEX";

            var tableForFilter = !isFilterForReferenced ? "K.`TABLE_NAME`" : "K.`REFERENCED_TABLE_NAME`";

            var sb = CreateSqlBuilder();

            sb.Append(
                $@"SELECT C.`CONSTRAINT_SCHEMA` AS `Schema`, K.`TABLE_NAME` AS `TableName`, K.CONSTRAINT_NAME AS `Name`, 
                        K.COLUMN_NAME AS `ColumnName`, K.`REFERENCED_TABLE_NAME` AS `ReferencedTableName`,K.`REFERENCED_COLUMN_NAME` AS `ReferencedColumnName`,
                        CASE RC.UPDATE_RULE WHEN 'CASCADE' THEN 1 ELSE 0 END AS `UpdateCascade`, 
                        CASE RC.`DELETE_RULE` WHEN 'CASCADE' THEN 1 ELSE 0 END AS `DeleteCascade`{commentColumn}
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA AND C.TABLE_NAME = K.TABLE_NAME AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                        JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC ON RC.CONSTRAINT_SCHEMA=C.CONSTRAINT_SCHEMA AND RC.CONSTRAINT_NAME=C.CONSTRAINT_NAME AND C.TABLE_NAME=RC.TABLE_NAME                        
                        {commentJoin}
                        WHERE C.CONSTRAINT_TYPE = 'FOREIGN KEY'
                        AND K.`TABLE_SCHEMA` ='{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, tableForFilter));

            return sb.Content;
        }

        #endregion

        #region Table Index

        public override Task<List<TableIndexItem>> GetTableIndexItemsAsync(SchemaInfoFilter filter = null,
            bool includePrimaryKey = false)
        {
            return GetDbObjectsAsync<TableIndexItem>(GetSqlForTableIndexItems(filter, includePrimaryKey));
        }

        public override Task<List<TableIndexItem>> GetTableIndexItemsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            return GetDbObjectsAsync<TableIndexItem>(dbConnection, GetSqlForTableIndexItems(filter, includePrimaryKey));
        }

        private string GetSqlForTableIndexItems(SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();
            var commentColumn = isSimpleMode ? "" : ",`INDEX_COMMENT` AS `Comment`";

            var sb = CreateSqlBuilder();

            sb.Append($@"SELECT TABLE_SCHEMA AS `Schema`,
	                    TABLE_NAME AS `TableName`,
	                    INDEX_NAME AS `Name`,
	                    COLUMN_NAME AS `ColumnName`,
                        CASE INDEX_NAME WHEN 'PRIMARY' THEN 1 ELSE 0 END AS `IsPrimary`,
	                    CASE  NON_UNIQUE WHEN 1 THEN 0 ELSE 1 END AS `IsUnique`,
                        INDEX_TYPE AS `Type`,
	                    SEQ_IN_INDEX  AS `Order`,    
	                    0 AS `IsDesc`{commentColumn}
	                    FROM INFORMATION_SCHEMA.STATISTICS                           
	                    WHERE INDEX_NAME NOT IN({(includePrimaryKey ? "" : "'PRIMARY',")} 'FOREIGN')                          
	                    AND TABLE_SCHEMA = '{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, "TABLE_NAME"));

            return sb.Content;
        }

        #endregion

        #region Table Trigger

        public override Task<List<TableTrigger>> GetTableTriggersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableTrigger>(GetSqlForTableTriggers(filter));
        }

        public override Task<List<TableTrigger>> GetTableTriggersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableTrigger>(dbConnection, GetSqlForTableTriggers(filter));
        }

        private string GetSqlForTableTriggers(SchemaInfoFilter filter = null)
        {
            var isSimpleMode = IsObjectFectchSimpleMode();

            var definitionClause =
                $@"CONVERT(CONCAT('CREATE TRIGGER {NotCreateIfExistsClause} `', TRIGGER_SCHEMA, '`.`', TRIGGER_NAME, '` ', ACTION_TIMING, ' ', EVENT_MANIPULATION, ' ON ', TRIGGER_SCHEMA, '.', EVENT_OBJECT_TABLE, ' FOR EACH ', ACTION_ORIENTATION, CHAR(10), ACTION_STATEMENT) USING UTF8)";

            var sb = CreateSqlBuilder();

            sb.Append($@"SELECT TRIGGER_NAME AS `Name`, TRIGGER_SCHEMA AS `Schema`, EVENT_OBJECT_TABLE AS `TableName`, 
                         {(isSimpleMode ? "''" : definitionClause)} AS `Definition`
                        FROM INFORMATION_SCHEMA.`TRIGGERS`
                        WHERE TRIGGER_SCHEMA = '{ConnectionInfo.Database}'");

            if (filter != null)
            {
                sb.Append(GetFilterNamesCondition(filter, filter.TableNames, "EVENT_OBJECT_TABLE"));

                if (filter.TableTriggerNames != null && filter.TableTriggerNames.Any())
                {
                    var strNames = StringHelper.GetSingleQuotedString(filter.TableTriggerNames);
                    sb.Append($"AND TRIGGER_NAME IN ({strNames})");
                }
            }

            sb.Append("ORDER BY TRIGGER_NAME");

            return sb.Content;
        }

        #endregion

        #region Table Constraint

        public override Task<List<TableConstraint>> GetTableConstraintsAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableConstraint>(GetSqlForTableConstraints(filter));
        }

        public override Task<List<TableConstraint>> GetTableConstraintsAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<TableConstraint>(dbConnection, GetSqlForTableConstraints(filter));
        }

        private string GetSqlForTableConstraints(SchemaInfoFilter filter = null)
        {
            var isLowDbVersion = IsLowDbVersion(GetDbVersion(), "8.0.16");

            if (isLowDbVersion)
            {
                return string.Empty;
            }

            var isSimpleMode = IsObjectFectchSimpleMode();
            var sb = CreateSqlBuilder();

            if (isSimpleMode)
            {
                sb.Append(
                    @"SELECT TC.CONSTRAINT_SCHEMA AS `Schema`,TC.TABLE_NAME AS `TableName`, TC.CONSTRAINT_NAME AS `Name`
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC 
                        WHERE CONSTRAINT_TYPE='CHECK'");
            }
            else
            {
                sb.Append(
                    $@"SELECT TC.CONSTRAINT_SCHEMA AS `Schema`,TC.TABLE_NAME AS `TableName`, TC.CONSTRAINT_NAME AS `Name`,
                         REPLACE(REPLACE(REPLACE(C.CHECK_CLAUSE,'\\',''),(SELECT CONCAT('_',DEFAULT_CHARACTER_SET_NAME) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = ""INFORMATION_SCHEMA""),''),
                         (SELECT CONCAT('_',DEFAULT_CHARACTER_SET_NAME) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{ConnectionInfo.Database}'),'') AS `Definition`
                         FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
                         JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS C ON TC.CONSTRAINT_CATALOG=C.CONSTRAINT_CATALOG AND TC.CONSTRAINT_SCHEMA=C.CONSTRAINT_SCHEMA AND TC.CONSTRAINT_NAME=C.CONSTRAINT_NAME
                         WHERE CONSTRAINT_TYPE='CHECK'");
            }

            sb.Append($"AND TC.CONSTRAINT_SCHEMA='{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.TableNames, "TC.TABLE_NAME"));

            sb.Append("ORDER BY TC.TABLE_NAME,TC.CONSTRAINT_NAME");

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
            var isSimpleMode = IsObjectFectchSimpleMode();

            var createViewClause =
                "CONCAT('CREATE VIEW `',TABLE_SCHEMA, '`.`', TABLE_NAME,  '` AS',CHAR(10),VIEW_DEFINITION)";

            var sb = CreateSqlBuilder();

            sb.Append(
                $@"SELECT TABLE_SCHEMA AS `Schema`,TABLE_NAME AS `Name`, {(isSimpleMode ? "''" : createViewClause)} AS `Definition` 
                        FROM INFORMATION_SCHEMA.`VIEWS`
                        WHERE TABLE_SCHEMA = '{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, filter?.ViewNames, "TABLE_NAME"));

            sb.Append("ORDER BY TABLE_NAME");

            return sb.Content;
        }

        #endregion

        #region Procedure

        public override Task<List<Procedure>> GetProceduresAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Procedure>(GetSqlForRoutines(DatabaseObjectType.Procedure, filter));
        }

        public override Task<List<Procedure>> GetProceduresAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<Procedure>(dbConnection, GetSqlForRoutines(DatabaseObjectType.Procedure, filter));
        }

        #endregion

        #region Routine Parameter

        public override Task<List<RoutineParameter>> GetFunctionParametersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(GetSqlForRoutineParameters(DatabaseObjectType.Function, filter));
        }

        public override Task<List<RoutineParameter>> GetFunctionParametersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(dbConnection,
                GetSqlForRoutineParameters(DatabaseObjectType.Function, filter));
        }

        public override Task<List<RoutineParameter>> GetProcedureParametersAsync(SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(GetSqlForRoutineParameters(DatabaseObjectType.Procedure,
                filter));
        }

        public override Task<List<RoutineParameter>> GetProcedureParametersAsync(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectsAsync<RoutineParameter>(dbConnection,
                GetSqlForRoutineParameters(DatabaseObjectType.Procedure, filter));
        }

        private string GetSqlForRoutineParameters(DatabaseObjectType databaseObjectType, SchemaInfoFilter filter = null)
        {
            var sb = new SqlBuilder();

            var isFunction = databaseObjectType == DatabaseObjectType.Function;

            var routineNames = isFunction ? filter?.FunctionNames : filter?.ProcedureNames;

            sb.Append(
                $@"SELECT p.`SPECIFIC_SCHEMA` AS `Schema`,p.`SPECIFIC_NAME` AS `RoutineName`, p.`PARAMETER_NAME` AS `Name`,
                        p.`DATA_TYPE` AS `DataType`,p.`CHARACTER_MAXIMUM_LENGTH` AS `MaxLength`, p.`NUMERIC_PRECISION` AS `Precision`,p.`NUMERIC_SCALE` AS `Scale`,p.`ORDINAL_POSITION` AS `Order`,
                        CASE WHEN p.`PARAMETER_MODE` ='IN' THEN 0 ELSE 1 END AS `IsOutput`
                        FROM INFORMATION_SCHEMA.`PARAMETERS` p
                        WHERE p.`PARAMETER_NAME` IS NOT NULL AND p.`SPECIFIC_SCHEMA` = '{ConnectionInfo.Database}'");

            sb.Append(GetFilterNamesCondition(filter, routineNames, "p.`SPECIFIC_NAME`"));

            sb.Append("ORDER BY p.`SPECIFIC_NAME`, p.`ORDINAL_POSITION`");

            return sb.Content;
        }

        #endregion

        #endregion

        #region Dependency

        #region View->Table Usage

        public override Task<List<ViewTableUsage>> GetViewTableUsages(SchemaInfoFilter filter = null,
            bool isFilterForReferenced = false)
        {
            return GetDbObjectUsagesAsync<ViewTableUsage>(GetSqlForViewTableUsages(filter, isFilterForReferenced));
        }

        public override Task<List<ViewTableUsage>> GetViewTableUsages(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            return GetDbObjectUsagesAsync<ViewTableUsage>(dbConnection,
                GetSqlForViewTableUsages(filter, isFilterForReferenced));
        }

        private string GetSqlForViewTableUsages(SchemaInfoFilter filter = null, bool isFilterForReferenced = false)
        {
            var sb = new SqlBuilder();

            sb.Append(
                @"SELECT vt.`VIEW_CATALOG` AS `ObjectCatalog`,vt.`VIEW_SCHEMA` AS `ObjectSchema`,vt.`VIEW_NAME` AS `ObjectName`,
                        vt.`TABLE_CATALOG` AS `RefObjectCatalog`,vt.`TABLE_SCHEMA` AS `RefObjectSchema`,vt.`TABLE_NAME` AS `RefObjectName`
                        FROM INFORMATION_SCHEMA.VIEW_TABLE_USAGE vt
                        WHERE 1=1");

            sb.Append(GetFilterSchemaCondition(filter,
                !isFilterForReferenced ? "vt.`VIEW_SCHEMA`" : "vt.`TABLE_SCHEMA`"));
            sb.Append(GetFilterNamesCondition(filter, !isFilterForReferenced ? filter?.ViewNames : filter?.TableNames,
                !isFilterForReferenced ? "vt.`VIEW_NAME`" : "vt.`TABLE_NAME`"));

            return sb.Content;
        }

        #endregion

        #region View->Column Usage

        public override Task<List<ViewColumnUsage>> GetViewColumnUsages(SchemaInfoFilter filter = null)
        {
            return GetDbObjectUsagesAsync<ViewColumnUsage>("");
        }

        public override Task<List<ViewColumnUsage>> GetViewColumnUsages(DbConnection dbConnection,
            SchemaInfoFilter filter = null)
        {
            return GetDbObjectUsagesAsync<ViewColumnUsage>(dbConnection, "");
        }

        #endregion

        #region Routine Script Usage

        public override Task<List<RoutineScriptUsage>> GetRoutineScriptUsages(SchemaInfoFilter filter = null,
            bool isFilterForReferenced = false, bool includeViewTableUsages = false)
        {
            return GetDbObjectUsagesAsync<RoutineScriptUsage>("");
        }

        public override Task<List<RoutineScriptUsage>> GetRoutineScriptUsages(DbConnection dbConnection,
            SchemaInfoFilter filter = null, bool isFilterForReferenced = false, bool includeViewTableUsages = false)
        {
            return GetDbObjectUsagesAsync<RoutineScriptUsage>(dbConnection, "");
        }

        #endregion

        #endregion

        #region Datbase Operation

        #endregion

        #region BulkCopy

        public override async Task BulkCopyAsync(DbConnection connection, DataTable dataTable,
            BulkCopyInfo bulkCopyInfo)
        {
            if (dataTable == null || dataTable.Rows.Count <= 0)
            {
                return;
            }

            var bulkCopy = new MySqlBulkCopy(connection as MySqlConnection,
                bulkCopyInfo.Transaction as MySqlTransaction)
            {
                DestinationTableName = GetQuotedString(bulkCopyInfo.DestinationTableName)
            };

            var i = 0;
            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, column.ColumnName));

                i++;
            }

            if (connection.State != ConnectionState.Open)
            {
                await OpenConnectionAsync(connection);
            }

            await bulkCopy.WriteToServerAsync(ConvertDataTable(dataTable, bulkCopyInfo),
                bulkCopyInfo.CancellationToken);
        }

        private DataTable ConvertDataTable(DataTable dataTable, BulkCopyInfo bulkCopyInfo)
        {
            var columns = dataTable.Columns.Cast<DataColumn>();

            if (!columns.Any(item => DataTypeHelper.IsGeometryType(item.DataType.Name.ToLower())
                                     || item.DataType.Name == nameof(BitArray)
                                     || item.DataType.Name == nameof(String)
                                     || item.DataType.Name == nameof(DateTime)
                                     || item.DataType == typeof(byte[])
                    //|| item.DataType == typeof(SdoGeometry)
                    //|| item.DataType == typeof(StGeometry)
                )
               )
            {
                return dataTable;
            }

            Func<DataColumn, TableColumn> getTableColumn = column =>
            {
                return bulkCopyInfo.Columns.FirstOrDefault(item => item.Name == column.ColumnName);
            };

            var dictColumnTypes = new Dictionary<string, Type>();
            var changedColumns = new Dictionary<int, DataTableColumnChangeInfo>();
            var changedValues = new Dictionary<(int RowIndex, int ColumnIndex), dynamic>();

            var rowIndex = 0;

            foreach (DataRow row in dataTable.Rows)
            {
                for (var i = 0; i < dataTable.Columns.Count; i++)
                {
                    var value = row[i];

                    if (value == null)
                    {
                        continue;
                    }

                    var type = value.GetType();

                    if (type == typeof(DBNull))
                    {
                        continue;
                    }

                    Type newColumnType = null;
                    object newValue = null;

                    var tableColumn = getTableColumn(dataTable.Columns[i]);
                    var dataType = tableColumn.DataType.ToLower();

                    if (DataTypeHelper.IsCharType(dataType) || DataTypeHelper.IsTextType(dataType))
                    {
                        newColumnType = typeof(string);
                        newValue = value == null ? null :
                            type == typeof(string) ? value?.ToString() : Convert.ChangeType(value, type);
                    }
                    else if (type == typeof(BitArray))
                    {
                        var bitArray = value as BitArray;
                        var bytes = new byte[bitArray.Length];
                        bitArray.CopyTo(bytes, 0);

                        newColumnType = typeof(byte[]);
                        newValue = bytes;
                    }
                    else if (DataTypeHelper.IsBinaryType(dataType) || dataType.ToLower().Contains("blob"))
                    {
                        newColumnType = typeof(byte[]);
                        newValue = value as byte[];
                    }
                    else if (dataType == "timestamp")
                    {
                        var dt = DateTime.Parse(value.ToString());

                        if (dt > Timestamp_Max_Value.ToLocalTime())
                        {
                            newColumnType = typeof(DateTime);
                            newValue = Timestamp_Max_Value.ToLocalTime();
                        }
                    }
                    else if (dataType == "geometry")
                    {
                        newColumnType = typeof(MySqlGeometry);

                        if (value is SqlGeography geography)
                        {
                            if (!geography.IsNull)
                            {
                                //newValue = SqlGeographyHelper.ToMySqlGeometry(geography);
                            }
                            else
                            {
                                newValue = DBNull.Value;
                            }
                        }
                        else if (value is SqlGeometry sqlGeom)
                        {
                            if (!sqlGeom.IsNull)
                            {
                                //newValue = SqlGeometryHelper.ToMySqlGeometry(sqlGeom);
                            }
                            else
                            {
                                newValue = DBNull.Value;
                            }
                        }
/*                        else if (value is PgGeom.Geometry geom)
                        {
                            //newValue = PostgresGeometryHelper.ToMySqlGeometry(geom);
                        }*/
                        /*                       else if (value is SdoGeometry sdo)
                                               {
                                                   newValue = OracleSdoGeometryHelper.ToMySqlGeometry(sdo);
                                               }
                                               else if (value is StGeometry st)
                                               {
                                                   newValue = OracleStGeometryHelper.ToMySqlGeometry(st);
                                               }*/
                        else if (value is byte[] bytes)
                        {
                            var sourcedDbType = bulkCopyInfo.SourceDatabaseType;

                            if (sourcedDbType == DatabaseType.MySql)
                            {
                                newValue = MySqlGeometry.FromMySql(bytes);
                            }
                        }
                        else if (value is string)
                        {
                            //newValue = SqlGeometryHelper.ToMySqlGeometry(value as string);
                        }
                    }

                    if (DataTypeHelper.IsGeometryType(dataType) && newColumnType != null && newValue == null)
                    {
                        newValue = DBNull.Value;
                    }

                    if (newColumnType != null && !changedColumns.ContainsKey(i))
                    {
                        changedColumns.Add(i, new DataTableColumnChangeInfo { Type = newColumnType });
                    }

                    if (newValue != null)
                    {
                        changedValues.Add((rowIndex, i), newValue);
                    }
                }

                rowIndex++;
            }

            if (changedColumns.Count == 0)
            {
                return dataTable;
            }

            var dtChanged = DataTableHelper.GetChangedDataTable(dataTable, changedColumns, changedValues);

            return dtChanged;
        }

        #endregion

        #region Sql Query Clause

        protected override string GetSqlForPagination(string tableName, string columnNames, string orderColumns,
            string whereClause, long pageNumber, int pageSize)
        {
            var startEndRowNumber = PaginationHelper.GetStartEndRowNumber(pageNumber, pageSize);

            var pagedSql = $@"SELECT {columnNames}
							  FROM {tableName}
                             {whereClause} 
                             ORDER BY {(!string.IsNullOrEmpty(orderColumns) ? orderColumns : GetDefaultOrder())}
                             LIMIT {startEndRowNumber.StartRowNumber - 1} , {pageSize}";

            return pagedSql;
        }

        public override string GetDefaultOrder()
        {
            return "1";
        }

        public override string GetLimitStatement(int limitStart, int limitCount)
        {
            return $"LIMIT {limitStart}, {limitCount}";
        }

        #endregion

        #region Parse Column & DataType

        public override string ParseColumn(Table table, TableColumn column)
        {
            var dataType = ParseDataType(column);
            var requiredClause = column.IsRequired ? "NOT NULL" : "NULL";
            var supportComputeColumn = IsSupportComputeColumn();
            var isChar = DataTypeHelper.IsCharType(dataType.ToLower());

            if (isChar || DataTypeHelper.IsTextType(dataType.ToLower()))
            {
                dataType += $" CHARACTER SET {DbCharset} COLLATE {DbCharsetCollation} ";
            }

            if (column.IsComputed && supportComputeColumn)
            {
                var computeExpression = GetColumnComputeExpression(column);

                return $"{GetQuotedString(column.Name)} {dataType} AS {computeExpression} {requiredClause}";
            }

            var identityClause = Option.TableScriptsGenerateOption.GenerateIdentity && column.IsIdentity
                ? "AUTO_INCREMENT"
                : "";
            var commentClause =
                !string.IsNullOrEmpty(column.Comment) && Option.TableScriptsGenerateOption.GenerateComment
                    ? $"COMMENT '{ReplaceSplitChar(ValueHelper.TransferSingleQuotation(column.Comment))}'"
                    : "";
            var defaultValueClause =
                Option.TableScriptsGenerateOption.GenerateDefaultValue && AllowDefaultValue(column) &&
                !string.IsNullOrEmpty(column.DefaultValue) && !ValueHelper.IsSequenceNextVal(column.DefaultValue)
                    ? " DEFAULT " + StringHelper.GetParenthesisedString(GetColumnDefaultValue(column))
                    : "";
            var scriptComment = string.IsNullOrEmpty(column.ScriptComment) ? "" : $"/*{column.ScriptComment}*/";

            return
                $"{GetQuotedString(column.Name)} {dataType} {requiredClause} {identityClause}{defaultValueClause} {scriptComment}{commentClause}";
        }

        public override string ParseDataType(TableColumn column)
        {
            var dataType = column.DataType;

            if (dataType.IndexOf("(", StringComparison.Ordinal) < 0)
            {
                var dataLength = GetColumnDataLength(column);

                if (!string.IsNullOrEmpty(dataLength))
                {
                    dataType += $"({dataLength})";
                }
            }

            return dataType.Trim();
        }

        public override string GetColumnDataLength(TableColumn column)
        {
            var dataType = column.DataType;
            var dataLength = string.Empty;

            var dataTypeInfo = GetDataTypeInfo(dataType);
            var isChar = DataTypeHelper.IsCharType(dataType);
            var isBinary = DataTypeHelper.IsBinaryType(dataType);

            var dataTypeSpec = GetDataTypeSpecification(dataTypeInfo.DataType);

            if (dataTypeSpec != null)
            {
                if (!string.IsNullOrEmpty(dataTypeSpec.Args))
                {
                    if (string.IsNullOrEmpty(dataTypeInfo.Args))
                    {
                        if (isChar || isBinary)
                        {
                            dataLength = column.MaxLength.ToString();
                        }
                        else if (!IsNoLengthDataType(dataType))
                        {
                            dataLength = GetDataTypePrecisionScale(column, dataTypeInfo.DataType);
                        }
                    }
                    else
                    {
                        dataLength = dataTypeInfo.Args;
                    }
                }
            }

            return dataLength;
        }

        private bool AllowDefaultValue(TableColumn column)
        {
            var dataType = column.DataType.ToLower();

            switch (dataType)
            {
                case "blob":
                case "tinyblob":
                case "mediumblob":
                case "longblob":
                case "text":
                case "tinytext":
                case "mediumtext":
                case "longtext":
                case "geometry":
                case "geomcollection":
                case "point":
                case "multipoint":
                case "linestring":
                case "multilinestring":
                case "polygon":
                case "multipolygon":
                case "json":
                    return false;
            }

            return true;
        }

        #endregion
    }
}