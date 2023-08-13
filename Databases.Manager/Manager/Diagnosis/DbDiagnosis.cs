using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Databases.Converter.Helper;
using Databases.Handlers;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Interpreter.Utility.Model;
using Databases.Manager.Model;
using Databases.Manager.Model.Diagnose;
using Databases.Manager.Script;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Dependency;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.Model.Schema;
using Databases.SqlAnalyser.Model.Statement;

namespace Databases.Manager.Diagnosis
{
    public abstract class DbDiagnosis
    {
        protected ConnectionInfo connectionInfo;

        public FeedbackHandler OnFeedback;

        public DbDiagnosis(ConnectionInfo connectionInfo)
        {
            this.connectionInfo = connectionInfo;
        }

        public abstract DatabaseType DatabaseType { get; }
        public string Schema { get; set; }

        protected void Feedback(string message)
        {
            OnFeedback?.Invoke(new FeedbackInfo { InfoType = FeedbackInfoType.Info, Message = message, Owner = this });
        }

        #region Diagnose Table

        public virtual Task<TableDiagnoseResult> DiagnoseTable(TableDiagnoseType diagnoseType)
        {
            if (diagnoseType == TableDiagnoseType.SelfReferenceSame)
            {
                return DiagnoseSelfReferenceSameForTable();
            }

            if (diagnoseType == TableDiagnoseType.NotNullWithEmpty)
            {
                return DiagnoseNotNullWithEmptyForTable();
            }

            if (diagnoseType == TableDiagnoseType.WithLeadingOrTrailingWhitespace)
            {
                return DiagnoseWithLeadingOrTrailingWhitespaceForTable();
            }

            throw new NotSupportedException($"Not support diagnose for {diagnoseType}");
        }

        public virtual async Task<TableDiagnoseResult> DiagnoseNotNullWithEmptyForTable()
        {
            Feedback("Begin to diagnose not null fields with empty value...");

            var result = await DiagnoseTableColumn(TableDiagnoseType.NotNullWithEmpty);

            Feedback("End diagnose not null fields with empty value.");

            return result;
        }

        public virtual async Task<TableDiagnoseResult> DiagnoseWithLeadingOrTrailingWhitespaceForTable()
        {
            Feedback("Begin to diagnose character fields with leading or trailing whitespace...");

            var result = await DiagnoseTableColumn(TableDiagnoseType.WithLeadingOrTrailingWhitespace);

            Feedback("End diagnose character fields with leading or trailing whitespace.");

            return result;
        }

        private async Task<TableDiagnoseResult> DiagnoseTableColumn(TableDiagnoseType diagnoseType)
        {
            var result = new TableDiagnoseResult();

            var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, option);

            var filter = new SchemaInfoFilter { Schema = Schema };

            Feedback("Begin to get table columns...");

            var columns = await interpreter.GetTableColumnsAsync(filter);

            Feedback("End get table columns.");

            dynamic groups = null;

            if (diagnoseType == TableDiagnoseType.NotNullWithEmpty)
            {
                groups = columns.Where(item =>
                        DataTypeHelper.IsCharType(item.DataType) && !item.DataType.EndsWith("[]") && !item.IsNullable)
                    .GroupBy(item => new { item.Schema, item.TableName });
            }
            else if (diagnoseType == TableDiagnoseType.WithLeadingOrTrailingWhitespace)
            {
                groups = columns
                    .Where(item => DataTypeHelper.IsCharType(item.DataType) && !item.DataType.EndsWith("[]"))
                    .GroupBy(item => new { item.Schema, item.TableName });
            }

            using (var dbConnection = interpreter.CreateConnection())
            {
                foreach (var group in groups)
                foreach (TableColumn column in group)
                {
                    var countSql = "";

                    if (diagnoseType == TableDiagnoseType.NotNullWithEmpty)
                    {
                        countSql = GetTableColumnWithEmptyValueSql(interpreter, column, true);
                    }
                    else
                    {
                        countSql = GetTableColumnWithLeadingOrTrailingWhitespaceSql(interpreter, column, true);
                    }

                    Feedback(
                        $@"Begin to get invalid record count for column ""{column.Name}"" of table ""{column.TableName}""...");

                    var count = Convert.ToInt32(await interpreter.GetScalarAsync(dbConnection, countSql));

                    Feedback(
                        $@"End get invalid record count for column ""{column.Name}"" of table ""{column.TableName}"", the count is {count}.");

                    if (count > 0)
                    {
                        var sql = "";

                        if (diagnoseType == TableDiagnoseType.NotNullWithEmpty)
                        {
                            sql = GetTableColumnWithEmptyValueSql(interpreter, column, false);
                        }
                        else
                        {
                            sql = GetTableColumnWithLeadingOrTrailingWhitespaceSql(interpreter, column, false);
                        }

                        result.Details.Add(new TableDiagnoseResultDetail
                        {
                            DatabaseObject = column,
                            RecordCount = count,
                            Sql = sql
                        });
                    }
                }
            }

            return result;
        }

        public virtual async Task<TableDiagnoseResult> DiagnoseSelfReferenceSameForTable()
        {
            Feedback("Begin to diagnose self reference with same value...");

            var result = new TableDiagnoseResult();

            var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Details };

            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, option);

            Feedback("Begin to get foreign keys...");

            var filter = new SchemaInfoFilter { Schema = Schema };

            var foreignKeys = await interpreter.GetTableForeignKeysAsync(filter);

            Feedback("End get foreign keys.");

            var groups = foreignKeys.Where(item => item.ReferencedTableName == item.TableName)
                .GroupBy(item => new { item.Schema, item.TableName });

            using (var dbConnection = interpreter.CreateConnection())
            {
                foreach (var group in groups)
                foreach (var foreignKey in group)
                {
                    var countSql = GetTableColumnReferenceSql(interpreter, foreignKey, true);

                    Feedback(
                        $@"Begin to get invalid record count for foreign key ""{foreignKey.Name}"" of table ""{foreignKey.TableName}""...");

                    var count = Convert.ToInt32(await interpreter.GetScalarAsync(dbConnection, countSql));

                    Feedback(
                        $@"End get invalid record count for column ""{foreignKey.Name}"" of table ""{foreignKey.TableName}"", the count is {count}.");

                    if (count > 0)
                    {
                        result.Details.Add(new TableDiagnoseResultDetail
                        {
                            DatabaseObject = foreignKey,
                            RecordCount = count,
                            Sql = GetTableColumnReferenceSql(interpreter, foreignKey, false)
                        });
                    }
                }
            }

            Feedback("End diagnose self reference with same value.");

            return result;
        }

        public abstract string GetStringLengthFunction();
        public abstract string GetStringNullFunction();

        public static DbDiagnosis GetInstance(DatabaseType databaseType, ConnectionInfo connectionInfo)
        {
            return SqlHandler.GetHandler(databaseType).CreateDbDiagnosis(connectionInfo);
        }

        protected virtual string GetTableColumnWithEmptyValueSql(DbInterpreter interpreter, TableColumn column,
            bool isCount)
        {
            var tableName = $"{column.Schema}.{interpreter.GetQuotedString(column.TableName)}";
            var selectColumn = isCount
                ? $"{GetStringNullFunction()}(COUNT(1),0) AS {interpreter.GetQuotedString("Count")}"
                : "*";

            var sql =
                $"SELECT {selectColumn} FROM {tableName} WHERE {GetStringLengthFunction()}({interpreter.GetQuotedString(column.Name)})=0";

            return sql;
        }

        protected virtual string GetTableColumnWithLeadingOrTrailingWhitespaceSql(DbInterpreter interpreter,
            TableColumn column, bool isCount)
        {
            var tableName = $"{column.Schema}.{interpreter.GetQuotedString(column.TableName)}";
            var selectColumn = isCount
                ? $"{GetStringNullFunction()}(COUNT(1),0) AS {interpreter.GetQuotedString("Count")}"
                : "*";
            var columnName = interpreter.GetQuotedString(column.Name);
            var lengthFunName = GetStringLengthFunction();

            var sql =
                $"SELECT {selectColumn} FROM {tableName} WHERE {lengthFunName}(TRIM({columnName}))<{lengthFunName}({columnName})";

            if (interpreter.DatabaseType == DatabaseType.Postgres)
            {
                if (column.DataType == "character")
                {
                    sql += $" OR {lengthFunName}({columnName})<>{column.MaxLength}";
                }
            }

            return sql;
        }

        protected virtual string GetTableColumnReferenceSql(DbInterpreter interpreter, TableForeignKey foreignKey,
            bool isCount)
        {
            var tableName = $"{foreignKey.Schema}.{interpreter.GetQuotedString(foreignKey.TableName)}";
            var selectColumn = isCount
                ? $"{GetStringNullFunction()}(COUNT(1),0) AS {interpreter.GetQuotedString("Count")}"
                : "*";
            var whereClause = string.Join(" AND ",
                foreignKey.Columns.Select(item =>
                    $"{interpreter.GetQuotedString(item.ColumnName)}={interpreter.GetQuotedString(item.ReferencedColumnName)}"));

            var sql = $"SELECT {selectColumn} FROM {tableName} WHERE ({whereClause})";

            return sql;
        }

        #endregion

        #region Diagnose Scripts

        public virtual Task<List<ScriptDiagnoseResult>> DiagnoseScript(ScriptDiagnoseType diagnoseType)
        {
            if (diagnoseType == ScriptDiagnoseType.ViewColumnAliasWithoutQuotationChar)
            {
                return DiagnoseViewColumnAliasForScript();
            }

            if (diagnoseType == ScriptDiagnoseType.NameNotMatch)
            {
                return DiagnoseNameNotMatchForScript();
            }

            throw new NotSupportedException($"Not support diagnose for {diagnoseType}.");
        }

        public virtual async Task<List<ScriptDiagnoseResult>> DiagnoseViewColumnAliasForScript()
        {
            Feedback("Begin to diagnose column alias has no quotation char for view...");

            var results = await DiagnoseViewColumnAlias();

            Feedback("End diagnosecolumn alias has no quotation char for view.");

            return results;
        }

        public virtual async Task<List<ScriptDiagnoseResult>> DiagnoseNameNotMatchForScript()
        {
            Feedback("Begin to diagnose name not match in script...");

            var results = await DiagnoseNameNotMatch();

            Feedback("End diagnose name not match in script.");

            return results;
        }

        private async Task<List<ScriptDiagnoseResult>> DiagnoseViewColumnAlias()
        {
            var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Details };

            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, option);

            var filter = new SchemaInfoFilter { Schema = Schema };

            Feedback("Begin to get views...");

            var views = await interpreter.GetViewsAsync(filter);

            Feedback("End get views.");

            var results = await Task.Run(() => { return HandleColumnAliasWithoutQuotationChar(interpreter, views); });

            return results;
        }

        private List<ScriptDiagnoseResult> HandleColumnAliasWithoutQuotationChar(DbInterpreter interpreter,
            List<View> views)
        {
            var results = new List<ScriptDiagnoseResult>();

            Feedback("Begin to analyse column alias...");

            foreach (var view in views)
            {
                var definition = view.Definition;

                var wrappedDefinition = GetWrappedDefinition(definition);

                var sqlAnalyser = TranslateHelper.GetSqlAnalyser(DatabaseType, wrappedDefinition);

                sqlAnalyser.RuleAnalyser.Option.ParseTokenChildren = false;
                sqlAnalyser.RuleAnalyser.Option.ExtractFunctions = false;
                sqlAnalyser.RuleAnalyser.Option.ExtractFunctionChildren = false;

                var analyseResult = sqlAnalyser.Analyse<View>();

                if (analyseResult != null && !analyseResult.HasError)
                {
                    var selectStatement =
                        analyseResult.Script.Statements.FirstOrDefault(item => item is SelectStatement) as
                            SelectStatement;

                    if (selectStatement != null)
                    {
                        var result = new ScriptDiagnoseResult { DbObject = view };

                        result.Details.AddRange(ParseSelectStatementColumns(interpreter, selectStatement));

                        if (selectStatement.UnionStatements != null)
                        {
                            foreach (var union in selectStatement.UnionStatements)
                            {
                                result.Details.AddRange(ParseSelectStatementColumns(interpreter,
                                    union.SelectStatement));
                            }
                        }

                        if (result.Details.Count > 0)
                        {
                            results.Add(result);
                        }
                    }
                }
            }

            Feedback("End analyse column alias.");

            return results;
        }

        private List<ScriptDiagnoseResultDetail> ParseSelectStatementColumns(DbInterpreter interpreter,
            SelectStatement statement)
        {
            var details = new List<ScriptDiagnoseResultDetail>();

            var columns = statement.Columns;

            foreach (var col in columns)
            {
                if (col.Alias != null && !col.Alias.Symbol.StartsWith(interpreter.QuotationLeftChar.ToString()))
                {
                    var detail = new ScriptDiagnoseResultDetail
                    {
                        ObjectType = DatabaseObjectType.Column,
                        InvalidName = col.FieldName,
                        Name = interpreter.GetQuotedString(col.FieldName),
                        Index = col.Alias.StartIndex.Value
                    };

                    details.Add(detail);
                }
            }

            return details;
        }

        private async Task<List<ScriptDiagnoseResult>> DiagnoseNameNotMatch()
        {
            var results = new List<ScriptDiagnoseResult>();

            var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Details };

            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, option);

            var filter = new SchemaInfoFilter { Schema = Schema };

            List<View> views = null;
            List<Function> functions = null;
            List<Procedure> procedures = null;
            List<ViewColumnUsage> viewColumnUsages = null;
            List<RoutineScriptUsage> functionUsages = null;
            List<RoutineScriptUsage> procedureUsages = null;

            using (var connection = interpreter.CreateConnection())
            {
                await interpreter.OpenConnectionAsync(connection);

                interpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

                var tables = await interpreter.GetTablesAsync();

                interpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Details;

                #region View

                Feedback("Begin to get views...");

                views = await interpreter.GetViewsAsync(connection, filter);

                Feedback("End get views.");

                var viewNamesFilter = new SchemaInfoFilter
                    { Schema = Schema, ViewNames = views.Select(item => item.Name).ToArray() };

                if (DatabaseType == DatabaseType.SqlServer)
                {
                    viewColumnUsages = await interpreter.GetViewColumnUsages(connection, viewNamesFilter);
                }

                #endregion

                #region Function

                Feedback("Begin to get functions...");

                functions = await interpreter.GetFunctionsAsync(connection, filter);

                Feedback("End get functions.");

                var functionNamesFilter = new SchemaInfoFilter
                {
                    DatabaseObjectType = DatabaseObjectType.Function,
                    FunctionNames = functions.Select(item => item.Name).ToArray()
                };

                if (DatabaseType == DatabaseType.SqlServer)
                {
                    functionUsages = await interpreter.GetRoutineScriptUsages(connection, functionNamesFilter);

                    await HandleRoutineScriptUsagesAsync(functionUsages, interpreter, connection, tables, views,
                        functions);
                }
                else if (DatabaseType == DatabaseType.MySql)
                {
                    var usages =
                        await GetRoutineScriptUsages(interpreter, connection, functions, tables, views, functions);

                    if (usages.Count > 0)
                    {
                        functionUsages = usages;
                    }
                }

                #endregion

                #region Procedure

                Feedback("Begin to get procedures...");

                procedures = await interpreter.GetProceduresAsync(connection, filter);

                Feedback("End get procedures.");

                var procedureNamesFilter = new SchemaInfoFilter
                {
                    DatabaseObjectType = DatabaseObjectType.Procedure,
                    ProcedureNames = procedures.Select(item => item.Name).ToArray()
                };

                if (DatabaseType == DatabaseType.SqlServer)
                {
                    procedureUsages = await interpreter.GetRoutineScriptUsages(connection, procedureNamesFilter);

                    await HandleRoutineScriptUsagesAsync(procedureUsages, interpreter, connection, tables, views,
                        functions, procedures);
                }
                else if (DatabaseType == DatabaseType.MySql)
                {
                    var usages = await GetRoutineScriptUsages(interpreter, connection, procedures, tables, views,
                        functions, procedures);

                    if (usages.Count > 0)
                    {
                        procedureUsages = usages;
                    }
                }

                #endregion
            }

            await Task.Run(() =>
            {
                if (views != null && viewColumnUsages != null)
                {
                    results.AddRange(DiagnoseNameNotMatchForViews(views, viewColumnUsages, interpreter.CommentString));
                }

                if (functions != null && functionUsages != null)
                {
                    results.AddRange(DiagnoseNameNotMatchForRoutineScripts(functions, functionUsages,
                        interpreter.CommentString));
                }

                if (procedures != null && procedureUsages != null)
                {
                    results.AddRange(DiagnoseNameNotMatchForRoutineScripts(procedures, procedureUsages,
                        interpreter.CommentString));
                }
            });

            return results;
        }

        private async Task<List<RoutineScriptUsage>> GetRoutineScriptUsages(DbInterpreter interpreter,
            DbConnection connection, IEnumerable<ScriptDbObject> scriptDbObjects, List<Table> tables, List<View> views,
            List<Function> functions = null, List<Procedure> procedures = null)
        {
            var tableNames = tables.Select(item => item.Name);
            var viewNames = views.Select(item => item.Name);
            var functionNames = functions == null ? null : functions.Select(item => item.Name);
            var procedureNames = procedures == null ? null : procedures.Select(item => item.Name);

            var usages = new List<RoutineScriptUsage>();

            foreach (var sdb in scriptDbObjects)
            {
                usages.AddRange(GetRoutineScriptUsages(sdb, tables, tableNames));
                usages.AddRange(GetRoutineScriptUsages(sdb, views, viewNames));

                if (functions != null)
                {
                    usages.AddRange(GetRoutineScriptUsages(sdb, functions, functionNames));
                }

                if (procedures != null)
                {
                    usages.AddRange(GetRoutineScriptUsages(sdb, procedures, procedureNames));
                }
            }

            if (usages.Count > 0)
            {
                await HandleRoutineScriptUsagesAsync(usages, interpreter, connection, tables, views, functions,
                    procedures);
            }

            return usages;
        }

        private List<RoutineScriptUsage> GetRoutineScriptUsages(ScriptDbObject scriptDbObject,
            IEnumerable<DatabaseObject> dbObjects, IEnumerable<string> dbObjectNames)
        {
            var usages = new List<RoutineScriptUsage>();

            foreach (var name in dbObjectNames)
            {
                var body = ScriptParser.ExtractScriptBody(scriptDbObject.Definition);

                if (Regex.IsMatch(body, $@"\b{name}\b", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    var usage = new RoutineScriptUsage
                    {
                        ObjectType = scriptDbObject.GetType().Name, ObjectSchema = scriptDbObject.Schema,
                        ObjectName = scriptDbObject.Name
                    };

                    var dbObj = dbObjects.FirstOrDefault(item => item.Name == name);

                    usage.RefObjectType = dbObj.GetType().Name;
                    usage.RefObjectSchema = dbObj.Schema;
                    usage.RefObjectName = dbObj.Name;

                    usages.Add(usage);
                }
            }

            return usages;
        }

        private async Task HandleRoutineScriptUsagesAsync(IEnumerable<RoutineScriptUsage> usages,
            DbInterpreter interpreter, DbConnection connection,
            List<Table> tables = null, List<View> views = null, List<Function> functions = null,
            List<Procedure> procedures = null
        )
        {
            var usageTables = usages.Where(item => item.RefObjectType == "Table");

            if (tables != null)
                //correct table name to defined if it's not same as defined
            {
                CorrectRefObjectName(usageTables, tables);
            }

            var tableNames = usageTables.Select(item => item.RefObjectName).ToArray();

            var tableNamesFilter = new SchemaInfoFilter { TableNames = tableNames };

            var tableColumns = await interpreter.GetTableColumnsAsync(connection, tableNamesFilter);

            foreach (var usage in usages)
            {
                if (usage.RefObjectType == "Table")
                {
                    usage.ColumnNames = tableColumns
                        .Where(item => item.Schema == usage.RefObjectSchema && item.TableName == usage.RefObjectName)
                        .Select(item => item.Name).ToList();
                }
            }

            if (views != null)
            {
                CorrectRefObjectName(usages.Where(item => item.RefObjectType == "View"), views);
            }

            if (functions != null)
            {
                CorrectRefObjectName(usages.Where(item => item.RefObjectType == "Function"), functions);
            }

            if (procedures != null)
            {
                CorrectRefObjectName(usages.Where(item => item.RefObjectType == "Procedure"), procedures);
            }
        }

        private void CorrectRefObjectName(IEnumerable<RoutineScriptUsage> usages, IEnumerable<DatabaseObject> dbObjects)
        {
            if (usages == null || dbObjects == null)
            {
                return;
            }

            foreach (var usage in usages)
            {
                var refObjectName = usage.RefObjectName;

                var dbObject = dbObjects.FirstOrDefault(item =>
                    item.Schema == usage.RefObjectSchema && item.Name.ToLower() == refObjectName.ToLower());

                if (dbObject != null && dbObject.Name != refObjectName)
                {
                    usage.RefObjectName = dbObject.Name;
                }
            }
        }

        private List<ScriptDiagnoseResult> DiagnoseNameNotMatchForViews(IEnumerable<ScriptDbObject> views,
            List<ViewColumnUsage> columnUsages, string commentString)
        {
            var usages = columnUsages.GroupBy(item => new
                {
                    item.ObjectCatalog, item.ObjectSchema, item.ObjectName, item.RefObjectCatalog, item.RefObjectSchema,
                    item.RefObjectName
                })
                .Select(item => new RoutineScriptUsage
                {
                    ObjectCatalog = item.Key.ObjectCatalog,
                    ObjectSchema = item.Key.ObjectSchema,
                    ObjectName = item.Key.ObjectName,
                    ObjectType = "View",
                    RefObjectType = "Table",
                    RefObjectCatalog = item.Key.RefObjectCatalog,
                    RefObjectSchema = item.Key.RefObjectSchema,
                    RefObjectName = item.Key.RefObjectName,
                    ColumnNames = item.Select(t => t.ColumnName).ToList()
                }).ToList();

            return DiagnoseNameNotMatchForRoutineScripts(views, usages, commentString);
        }

        private List<ScriptDiagnoseResult> DiagnoseNameNotMatchForRoutineScripts(
            IEnumerable<ScriptDbObject> routineScripts, List<RoutineScriptUsage> usages, string commentString)
        {
            var results = new List<ScriptDiagnoseResult>();

            if (usages.Count == 0)
            {
                return results;
            }

            foreach (var rs in routineScripts)
            {
                var result = new ScriptDiagnoseResult
                {
                    DbObject = rs
                };

                var details = new List<ScriptDiagnoseResultDetail>();

                var rsUsages = usages.Where(item => item.ObjectSchema == rs.Schema && item.ObjectName == rs.Name);

                var usageRefObjectNames = rsUsages.Select(item => item.RefObjectName).Distinct().ToList();
                var usageColumnNames = rsUsages.SelectMany(item => item.ColumnNames).ToList();

                details.AddRange(MatchUsageNames(rs.Definition, DatabaseObjectType.Table, usageRefObjectNames,
                    commentString));
                details.AddRange(MatchUsageNames(rs.Definition, DatabaseObjectType.Column, usageColumnNames,
                    commentString));

                if (details.Count > 0)
                {
                    result.Details = details;
                    results.Add(result);
                }
            }

            return results;
        }

        private string GetWrappedDefinition(string definition)
        {
            return definition.Replace(Environment.NewLine, "\n");
        }

        private List<ScriptDiagnoseResultDetail> MatchUsageNames(string definition,
            DatabaseObjectType databaseObjectType, List<string> names, string commentString)
        {
            var details = new List<ScriptDiagnoseResultDetail>();

            if (names.Count == 0)
            {
                return details;
            }

            var pattern = $@"\b({string.Join("|", names)})\b";

            var wrappedDefinition = GetWrappedDefinition(definition);

            var textContentInfo = ScriptParser.GetContentInfo(wrappedDefinition, "\n", commentString);
            var commentLines = textContentInfo.Lines.Where(item => item.Type == TextLineType.Comment);

            var matches = Regex.Matches(wrappedDefinition, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var beginAsIndex = ScriptParser.GetBeginAsIndex(wrappedDefinition);

            foreach (Match match in matches)
            {
                var value = match.Value;

                if (beginAsIndex > 0 && match.Index < beginAsIndex)
                {
                    continue;
                }

                //check whether the value is in comment line
                if (commentLines.Any(item =>
                        match.Index > item.FirstCharIndex && match.Index < item.FirstCharIndex + item.Length))
                {
                    continue;
                }

                if (names.Any(item => item.ToLower() == value.ToLower()) && !names.Any(item => item == value))
                {
                    var detail = new ScriptDiagnoseResultDetail
                        { ObjectType = databaseObjectType, Index = match.Index, InvalidName = value };

                    var name = names.FirstOrDefault(item => item.ToLower() == value.ToLower());

                    detail.Name = name;

                    details.Add(detail);
                }
            }

            return details;
        }

        #endregion
    }
}