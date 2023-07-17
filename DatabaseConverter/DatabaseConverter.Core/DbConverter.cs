using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DatabaseConverter.Core.Model;
using DatabaseConverter.Model;
using DatabaseConverter.Profile;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public class DbConverter : IDisposable
    {
        private IObserver<FeedbackInfo> observer;
        private DbTransaction transaction;
        private DatabaseObject translateDbObject;

        public DbConverter(DbConveterInfo source, DbConveterInfo target)
        {
            Source = source;
            Target = target;

            Init();
        }

        public DbConverter(DbConveterInfo source, DbConveterInfo target, DbConverterOption option)
        {
            Source = source;
            Target = target;

            if (option != null) Option = option;

            Init();
        }

        public bool HasError { get; private set; }

        public bool IsBusy { get; private set; }

        public bool CancelRequested { get; private set; }

        public DbConveterInfo Source { get; set; }
        public DbConveterInfo Target { get; set; }

        public DbConverterOption Option { get; set; } = new DbConverterOption();

        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public void Dispose()
        {
            Source = null;
            Target = null;
            transaction = null;
        }

        public event FeedbackHandler OnFeedback;

        private void Init()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public Task<DbConvertResult> Translate(DatabaseObject dbObject)
        {
            translateDbObject = dbObject;

            var schemaInfo = SchemaInfoHelper.GetSchemaInfoByDbObject(dbObject);

            return InternalConvert(schemaInfo, dbObject.Schema);
        }

        public Task<DbConvertResult> Convert(SchemaInfo schemaInfo = null, string schema = null)
        {
            return InternalConvert(schemaInfo, schema);
        }

        private async Task<DbConvertResult> InternalConvert(SchemaInfo schemaInfo = null, string schema = null)
        {
            var result = new DbConvertResult();

            var continuedWhenErrorOccured = false;

            var mode = Option.GenerateScriptMode;

            var schemaModeOnly = mode == GenerateScriptMode.Schema;
            var dataModeOnly = mode == GenerateScriptMode.Data;

            var onlyForTranslate = Option.OnlyForTranslate;
            var onlyForTableCopy = Option.OnlyForTableCopy;
            var executeScriptOnTargetServer = Option.ExecuteScriptOnTargetServer;

            var sourceInterpreter = Source.DbInterpreter;
            var targetInterpreter = Target.DbInterpreter;

            sourceInterpreter.Subscribe(observer);
            targetInterpreter.Subscribe(observer);

            var sourceDbType = sourceInterpreter.DatabaseType;
            var targetDbType = targetInterpreter.DatabaseType;

            var sourceInterpreterOption = sourceInterpreter.Option;
            var targetInterpreterOption = targetInterpreter.Option;

            sourceInterpreterOption.BulkCopy = Option.BulkCopy;
            sourceInterpreterOption.GetTableAllObjects = false;
            targetInterpreterOption.GetTableAllObjects = false;

            if (dataModeOnly) sourceInterpreterOption.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

            targetInterpreterOption.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

            #region Schema filter

            var databaseObjectType = (DatabaseObjectType)Enum.GetValues(typeof(DatabaseObjectType)).Cast<int>().Sum();

            if (schemaInfo != null && !sourceInterpreterOption.GetTableAllObjects
                                   && (schemaInfo.TableTriggers == null || schemaInfo.TableTriggers.Count == 0))
                databaseObjectType = databaseObjectType ^ DatabaseObjectType.Trigger;

            if (Source.DatabaseObjectType != DatabaseObjectType.None)
                databaseObjectType = databaseObjectType & Source.DatabaseObjectType;

            var filter = new SchemaInfoFilter { Strict = true, DatabaseObjectType = databaseObjectType };

            if (schema != null) filter.Schema = schema;

            SchemaInfoHelper.SetSchemaInfoFilterValues(filter, schemaInfo);

            #endregion

            var sourceSchemaInfo = await sourceInterpreter.GetSchemaInfoAsync(filter);

            if (sourceInterpreter.HasError)
            {
                result.InfoType = DbConvertResultInfoType.Error;
                result.Message = "Source database interpreter has error occured.";
                return result;
            }

            sourceSchemaInfo.TableColumns =
                DbObjectHelper.ResortTableColumns(sourceSchemaInfo.Tables, sourceSchemaInfo.TableColumns);

            #region Check whether database objects already existed.

            List<TableColumn> existedTableColumns = null;

            if (DbInterpreter.Setting.NotCreateIfExists && !onlyForTranslate && !onlyForTableCopy)
                if (!dataModeOnly)
                {
                    targetInterpreterOption.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

                    var targetSchema = await targetInterpreter.GetSchemaInfoAsync(filter);

                    existedTableColumns = targetSchema.TableColumns.Where(item =>
                            sourceSchemaInfo.TableColumns.Any(
                                t => SchemaInfoHelper.IsSameTableColumnIgnoreCase(t, item)))
                        .ToList();

                    SchemaInfoHelper.ExcludeExistingObjects(sourceSchemaInfo, targetSchema);
                }

            if (!onlyForTranslate && existedTableColumns == null && !schemaModeOnly)
            {
                var columnFilter = new SchemaInfoFilter { TableNames = filter.TableNames };

                existedTableColumns = await targetInterpreter.GetTableColumnsAsync(columnFilter);
            }

            #endregion

            #region User defined type handle

            var utypes = new List<UserDefinedType>();

            if (!dataModeOnly)
                if (sourceDbType != targetDbType)
                {
                    utypes = await sourceInterpreter.GetUserDefinedTypesAsync();

                    if (Option.UseOriginalDataTypeIfUdtHasOnlyOneAttr)
                        if (utypes != null && utypes.Count > 0)
                            foreach (var column in sourceSchemaInfo.TableColumns)
                            {
                                var utype = utypes.FirstOrDefault(item => item.Name == column.DataType);

                                if (utype != null && utype.Attributes.Count == 1)
                                {
                                    var attr = utype.Attributes.First();

                                    column.DataType = attr.DataType;
                                    column.MaxLength = attr.MaxLength;
                                    column.Precision = attr.Precision;
                                    column.Scale = attr.Scale;
                                    column.IsUserDefined = false;
                                }
                            }
                }

            #endregion

            var targetSchemaInfo = SchemaInfoHelper.Clone(sourceSchemaInfo);

            #region Table copy handle

            if (onlyForTableCopy)
            {
                if (Source.TableNameMappings != null && Source.TableNameMappings.Count > 0)
                    SchemaInfoHelper.MapTableNames(targetSchemaInfo, Source.TableNameMappings);

                if (Option.RenameTableChildren) SchemaInfoHelper.RenameTableChildren(targetSchemaInfo);

                if (Option.IgnoreNotSelfForeignKey)
                    targetSchemaInfo.TableForeignKeys = targetSchemaInfo.TableForeignKeys
                        .Where(item => item.TableName == item.ReferencedTableName).ToList();
            }

            #endregion

            var targetDbScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(targetInterpreter);

            #region Create schema if not exists

            if (!dataModeOnly && Option.CreateSchemaIfNotExists &&
                (targetDbType == DatabaseType.SqlServer || targetDbType == DatabaseType.Postgres))
                using (var dbConnection = targetInterpreter.CreateConnection())
                {
                    if (dbConnection.State != ConnectionState.Open) await dbConnection.OpenAsync();

                    var sourceSchemas = (await sourceInterpreter.GetDatabaseSchemasAsync()).Select(item => item.Name);
                    var targetSchemas =
                        (await targetInterpreter.GetDatabaseSchemasAsync(dbConnection)).Select(item => item.Name);

                    var notExistsSchemas = sourceSchemas.Where(item => item != sourceInterpreter.DefaultSchema)
                        .Select(item => item)
                        .Union(Option.SchemaMappings.Select(item => item.TargetSchema))
                        .Except(targetSchemas.Select(item => item)).Distinct();

                    foreach (var schemaName in notExistsSchemas)
                    {
                        var createSchemaScript = targetDbScriptGenerator
                            .CreateSchema(new DatabaseSchema { Name = schemaName }).Content;

                        await targetInterpreter.ExecuteNonQueryAsync(dbConnection, GetCommandInfo(createSchemaScript));
                    }

                    if (Option.SchemaMappings.Count == 1 && Option.SchemaMappings.First().SourceSchema == "")
                        Option.SchemaMappings.Clear();

                    foreach (var ss in sourceSchemas)
                    {
                        var mappedSchema = SchemaInfoHelper.GetMappedSchema(ss, Option.SchemaMappings);

                        if (string.IsNullOrEmpty(mappedSchema))
                        {
                            var targetSchema = ss == sourceInterpreter.DefaultSchema
                                ? targetInterpreter.DefaultSchema
                                : ss;

                            Option.SchemaMappings.Add(new SchemaMappingInfo
                                { SourceSchema = ss, TargetSchema = targetSchema });
                        }
                    }
                }

            #endregion

            ConvertSchema(sourceInterpreter, targetInterpreter, targetSchemaInfo);

            #region Translate

            var translateEngine = new TranslateEngine(sourceSchemaInfo, targetSchemaInfo, sourceInterpreter,
                targetInterpreter, Option);

            translateEngine.ContinueWhenErrorOccurs =
                Option.ContinueWhenErrorOccurs || (!executeScriptOnTargetServer && !onlyForTranslate);

            var translateDbObjectType = TranslateEngine.SupportDatabaseObjectType;

            translateEngine.UserDefinedTypes = utypes;
            translateEngine.ExistedTableColumns = existedTableColumns;

            translateEngine.Subscribe(observer);

            await Task.Run(() => translateEngine.Translate(translateDbObjectType));

            result.TranslateResults = translateEngine.TranslateResults;

            #endregion

            #region Handle names of primary key and index

            if (!dataModeOnly && targetSchemaInfo.Tables.Any())
            {
                if (Option.EnsurePrimaryKeyNameUnique)
                {
                    SchemaInfoHelper.EnsurePrimaryKeyNameUnique(targetSchemaInfo);

                    if (sourceDbType == DatabaseType.MySql)
                        SchemaInfoHelper.ForceRenameMySqlPrimaryKey(targetSchemaInfo);
                }

                if (Option.EnsureIndexNameUnique) SchemaInfoHelper.EnsureIndexNameUnique(targetSchemaInfo);
            }

            #endregion

            var generateIdentity = targetInterpreterOption.TableScriptsGenerateOption.GenerateIdentity;

            var script = "";

            DataTransferErrorProfile dataErrorProfile = null;

            Script currentScript = null;

            using (var dbConnection = executeScriptOnTargetServer ? targetInterpreter.CreateConnection() : null)
            {
                ScriptBuilder scriptBuilder = null;

                if (!dataModeOnly)
                {
                    #region Oracle name length of low database version handle

                    if (targetDbType == DatabaseType.Oracle)
                        if (!onlyForTranslate)
                        {
                            var serverVersion = targetInterpreter.ConnectionInfo?.ServerVersion;
                            var isLowDbVersion = !string.IsNullOrEmpty(serverVersion)
                                ? targetInterpreter.IsLowDbVersion()
                                : targetInterpreter.IsLowDbVersion(dbConnection);

                            if (isLowDbVersion) SchemaInfoHelper.RistrictNameLength(targetSchemaInfo, 30);
                        }

                    #endregion

                    scriptBuilder = targetDbScriptGenerator.GenerateSchemaScripts(targetSchemaInfo);

                    if (onlyForTranslate)
                        if (!(translateDbObject is
                                ScriptDbObject)) //script db object uses script translator which uses event to feed back to ui.
                            result.TranslateResults.Add(new TranslateResult
                            {
                                DbObjectType = DbObjectHelper.GetDatabaseObjectType(translateDbObject),
                                DbObjectName = translateDbObject.Name, Data = scriptBuilder.ToString()
                            });
                }

                if (onlyForTranslate)
                {
                    result.InfoType = DbConvertResultInfoType.Information;
                    result.Message = "Translate has finished";

                    return result;
                }

                IsBusy = true;
                var canCommit = false;

                if (executeScriptOnTargetServer)
                {
                    if (dbConnection.State != ConnectionState.Open) await dbConnection.OpenAsync();

                    if (Option.UseTransaction)
                    {
                        //this.transaction = await dbConnection.BeginTransactionAsync();
                        transaction = dbConnection.BeginTransaction();
                        canCommit = true;
                    }
                }

                #region Schema sync

                if (scriptBuilder != null && executeScriptOnTargetServer)
                {
                    var scripts = scriptBuilder.Scripts;

                    if (scripts.Count == 0)
                    {
                        Feedback(targetInterpreter, "The script to create schema is empty.");

                        IsBusy = false;

                        result.InfoType = DbConvertResultInfoType.Information;
                        result.Message = "No any script to execute.";

                        return result;
                    }

                    targetInterpreter.Feedback(FeedbackInfoType.Info, "Begin to sync schema...");

                    try
                    {
                        if (!Option.SplitScriptsToExecute)
                        {
                            targetInterpreter.Feedback(FeedbackInfoType.Info, script);

                            await targetInterpreter.ExecuteNonQueryAsync(dbConnection,
                                GetCommandInfo(script, null, transaction));
                        }
                        else
                        {
                            Func<Script, bool> isValidScript = s =>
                            {
                                return !(s is NewLineSript || s is SpliterScript ||
                                         string.IsNullOrEmpty(s.Content) ||
                                         s.Content == targetInterpreter.ScriptsDelimiter);
                            };

                            var count = scripts.Where(item => isValidScript(item)).Count();
                            var i = 0;

                            foreach (var s in scripts)
                            {
                                currentScript = s;

                                var isView = s.ObjectType == nameof(View);
                                var isRoutineScript = IsRoutineScript(s);
                                var isRoutineScriptOrView = isRoutineScript || isView;

                                if (!isValidScript(s)) continue;

                                var sql = s.Content?.Trim();

                                if (!string.IsNullOrEmpty(sql) && sql != targetInterpreter.ScriptsDelimiter)
                                {
                                    i++;

                                    if (!isRoutineScript && targetInterpreter.ScriptsDelimiter.Length == 1 &&
                                        sql.EndsWith(targetInterpreter.ScriptsDelimiter))
                                        sql = sql.TrimEnd(targetInterpreter.ScriptsDelimiter.ToArray());

                                    if (!targetInterpreter.HasError ||
                                        (isRoutineScriptOrView && Option.ContinueWhenErrorOccurs))
                                    {
                                        targetInterpreter.Feedback(FeedbackInfoType.Info,
                                            $"({i}/{count}), executing:{Environment.NewLine} {sql}");

                                        var commandInfo = GetCommandInfo(sql, null, transaction);

                                        commandInfo.ContinueWhenErrorOccurs =
                                            isRoutineScriptOrView && Option.ContinueWhenErrorOccurs;

                                        await targetInterpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);

                                        if (commandInfo.HasError)
                                        {
                                            HasError = true;

                                            if (!Option.ContinueWhenErrorOccurs) break;

                                            if (isRoutineScriptOrView) continuedWhenErrorOccured = true;
                                        }

                                        if (commandInfo.TransactionRollbacked) canCommit = false;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        targetInterpreter.CancelRequested = true;

                        Rollback(ex);

                        var sourceConnectionInfo = sourceInterpreter.ConnectionInfo;
                        var targetConnectionInfo = targetInterpreter.ConnectionInfo;

                        var schemaTransferException = new SchemaTransferException(ex)
                        {
                            SourceServer = sourceConnectionInfo.Server,
                            SourceDatabase = sourceConnectionInfo.Database,
                            TargetServer = targetConnectionInfo.Server,
                            TargetDatabase = targetConnectionInfo.Database
                        };

                        var res = HandleError(schemaTransferException);

                        result.InfoType = res.InfoType;
                        result.Message = res.Message;
                    }

                    targetInterpreter.Feedback(FeedbackInfoType.Info, "End sync schema.");
                }

                #endregion

                #region Data sync

                if (!schemaModeOnly)
                    await SyncData(sourceInterpreter, targetInterpreter, dbConnection, sourceSchemaInfo,
                        targetSchemaInfo, targetDbScriptGenerator, result);

                #endregion

                if (transaction != null && transaction.Connection != null && !CancelRequested && canCommit)
                    transaction.Commit();

                IsBusy = false;
            }

            if (dataErrorProfile != null && !HasError && !CancelRequested)
                DataTransferErrorProfileManager.Remove(dataErrorProfile);

            if (HasError)
            {
                if (continuedWhenErrorOccured)
                {
                    result.InfoType = DbConvertResultInfoType.Warnning;
                    result.Message = $"Convert has finished,{Environment.NewLine}but some errors occured.";
                }
                else
                {
                    result.InfoType = DbConvertResultInfoType.Error;

                    if (string.IsNullOrEmpty(result.Message)) result.Message = "Convert failed.";
                }
            }
            else
            {
                result.InfoType = DbConvertResultInfoType.Information;
                result.Message = "Convert has finished.";
            }

            return result;
        }

        private async Task SyncData(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            DbConnection dbConnection, SchemaInfo sourceSchemaInfo, SchemaInfo targetSchemaInfo,
            DbScriptGenerator targetDbScriptGenerator, DbConvertResult result)
        {
            var sourceDbType = sourceInterpreter.DatabaseType;
            var targetDbType = targetInterpreter.DatabaseType;

            var mode = Option.GenerateScriptMode;

            var schemaModeOnly = mode == GenerateScriptMode.Schema;
            var dataModeOnly = mode == GenerateScriptMode.Data;

            var sourceInterpreterOption = sourceInterpreter.Option;
            var targetInterpreterOption = targetInterpreter.Option;

            var executeScriptOnTargetServer = Option.ExecuteScriptOnTargetServer;
            var generateIdentity = targetInterpreterOption.TableScriptsGenerateOption.GenerateIdentity;

            if (!targetInterpreter.HasError && !schemaModeOnly && sourceSchemaInfo.Tables.Count > 0)
            {
                if (targetDbType == DatabaseType.Oracle && generateIdentity)
                    if (!targetInterpreter.IsLowDbVersion())
                    {
                        sourceInterpreter.Option.ExcludeIdentityForData = true;
                        targetInterpreter.Option.ExcludeIdentityForData = true;
                    }

                var identityTableColumns = new List<TableColumn>();

                if (generateIdentity)
                    identityTableColumns = targetSchemaInfo.TableColumns.Where(item => item.IsIdentity).ToList();

                //await this.SetIdentityEnabled(identityTableColumns, targetInterpreter, targetDbScriptGenerator, dbConnection, transaction, false);

                if (executeScriptOnTargetServer ||
                    targetInterpreter.Option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                {
                    var dictTableDataTransferredCount = new Dictionary<Table, long>();

                    sourceInterpreter.OnDataRead += async tableDataReadInfo =>
                    {
                        if (!HasError)
                        {
                            var table = tableDataReadInfo.Table;
                            var columns = tableDataReadInfo.Columns;

                            try
                            {
                                var targetTableSchema = Option.SchemaMappings
                                    .Where(item => item.SourceSchema == table.Schema).FirstOrDefault()?.TargetSchema;

                                if (string.IsNullOrEmpty(targetTableSchema))
                                    targetTableSchema = targetInterpreter.DefaultSchema;

                                var targetTableAndColumns = GetTargetTableColumns(targetSchemaInfo, targetTableSchema,
                                    table, columns);

                                if (targetTableAndColumns.Table == null || targetTableAndColumns.Columns == null)
                                    return;

                                var data = tableDataReadInfo.Data;

                                if (executeScriptOnTargetServer)
                                {
                                    var dataTable = tableDataReadInfo.DataTable;

                                    if (Option.BulkCopy && targetInterpreter.SupportBulkCopy)
                                    {
                                        var bulkCopyInfo = GetBulkCopyInfo(table, targetSchemaInfo, transaction);

                                        if (targetDbType == DatabaseType.Oracle)
                                            if (columns.Any(item =>
                                                    item.DataType.ToLower().Contains("datetime2") ||
                                                    item.DataType.ToLower().Contains("timestamp")))
                                                bulkCopyInfo.DetectDateTimeTypeByValues = true;

                                        if (Option.ConvertComputeColumnExpression)
                                        {
                                            var dataColumns = dataTable.Columns.OfType<DataColumn>();

                                            foreach (var column in bulkCopyInfo.Columns)
                                                if (column.IsComputed &&
                                                    dataColumns.Any(item => item.ColumnName == column.Name))
                                                    dataTable.Columns.Remove(column.Name);
                                        }

                                        await targetInterpreter.BulkCopyAsync(dbConnection, dataTable, bulkCopyInfo);
                                    }
                                    else
                                    {
                                        var scriptResult = GenerateScripts(targetDbScriptGenerator,
                                            targetTableAndColumns, data);

                                        var script = scriptResult.Script;

                                        var delimiter = ");" + Environment.NewLine;

                                        if (!script.Contains(delimiter))
                                        {
                                            await targetInterpreter.ExecuteNonQueryAsync(dbConnection,
                                                GetCommandInfo(script, scriptResult.Paramters, transaction));
                                        }
                                        else
                                        {
                                            var items = script.SplitByString(delimiter);

                                            var count = 0;

                                            foreach (var item in items)
                                            {
                                                count++;

                                                var cmd = count < items.Length
                                                    ? (item + delimiter).Trim().Trim(';')
                                                    : item;

                                                await targetInterpreter.ExecuteNonQueryAsync(dbConnection,
                                                    GetCommandInfo(cmd, scriptResult.Paramters, transaction));
                                            }
                                        }
                                    }

                                    if (!dictTableDataTransferredCount.ContainsKey(table))
                                        dictTableDataTransferredCount.Add(table, dataTable.Rows.Count);
                                    else
                                        dictTableDataTransferredCount[table] += dataTable.Rows.Count;

                                    var transferredCount = dictTableDataTransferredCount[table];

                                    var percent = transferredCount * 1.0 / tableDataReadInfo.TotalCount * 100;

                                    var strPercent = percent == (int)percent
                                        ? percent + "%"
                                        : (percent / 100).ToString("P2");

                                    targetInterpreter.FeedbackInfo(
                                        $"Table \"{table.Name}\":{dataTable.Rows.Count} records transferred.({transferredCount}/{tableDataReadInfo.TotalCount},{strPercent})");
                                }
                                else
                                {
                                    GenerateScripts(targetDbScriptGenerator, targetTableAndColumns, data);
                                }
                            }
                            catch (Exception ex)
                            {
                                sourceInterpreter.CancelRequested = true;

                                Rollback(ex);

                                var sourceConnectionInfo = sourceInterpreter.ConnectionInfo;
                                var targetConnectionInfo = targetInterpreter.ConnectionInfo;

                                var mappedTableName = GetMappedTableName(table.Name);

                                var dataTransferException = new DataTransferException(ex)
                                {
                                    SourceServer = sourceConnectionInfo.Server,
                                    SourceDatabase = sourceConnectionInfo.Database,
                                    SourceObject = table.Name,
                                    TargetServer = targetConnectionInfo.Server,
                                    TargetDatabase = targetConnectionInfo.Database,
                                    TargetObject = mappedTableName
                                };

                                if (!Option.UseTransaction)
                                    DataTransferErrorProfileManager.Save(new DataTransferErrorProfile
                                    {
                                        SourceServer = sourceConnectionInfo.Server,
                                        SourceDatabase = sourceConnectionInfo.Database,
                                        SourceTableName = table.Name,
                                        TargetServer = targetConnectionInfo.Server,
                                        TargetDatabase = targetConnectionInfo.Database,
                                        TargetTableName = mappedTableName
                                    });

                                var res = HandleError(dataTransferException);

                                result.InfoType = DbConvertResultInfoType.Error;
                                result.Message = res.Message;
                            }
                        }
                    };
                }

                var sourceDbScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(sourceInterpreter);

                await sourceDbScriptGenerator.GenerateDataScriptsAsync(sourceSchemaInfo);

                //await this.SetIdentityEnabled(identityTableColumns, targetInterpreter, targetDbScriptGenerator, dbConnection, transaction, true);
            }
        }

        private bool IsRoutineScript(Script script)
        {
            return script.ObjectType == nameof(Function) || script.ObjectType == nameof(Procedure) ||
                   script.ObjectType == nameof(TableTrigger);
        }

        private (Dictionary<string, object> Paramters, string Script) GenerateScripts(
            DbScriptGenerator targetDbScriptGenerator, (Table Table, List<TableColumn> Columns) targetTableAndColumns,
            List<Dictionary<string, object>> data)
        {
            var sb = new StringBuilder();

            var paramters = targetDbScriptGenerator.AppendDataScripts(sb, targetTableAndColumns.Table,
                targetTableAndColumns.Columns, new Dictionary<long, List<Dictionary<string, object>>> { { 1, data } });

            var script = sb.ToString().Trim().Trim(';');

            return (paramters, script);
        }

        #region Not use it currently, because bulkcopy doesn't care identity and insert script has excluded the identity columns.

        private async Task SetIdentityEnabled(IEnumerable<TableColumn> identityTableColumns,
            DbInterpreter dbInterpreter, DbScriptGenerator scriptGenerator,
            DbConnection dbConnection, DbTransaction transaction, bool enabled)
        {
            foreach (var item in identityTableColumns)
            {
                var sql = scriptGenerator.SetIdentityEnabled(item, enabled).Content;

                if (!string.IsNullOrEmpty(sql))
                {
                    var commandInfo = GetCommandInfo(sql, null, transaction);
                    commandInfo.ContinueWhenErrorOccurs = true;

                    await dbInterpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
                }
            }
        }

        #endregion

        private void Rollback(Exception ex = null)
        {
            if (transaction != null && transaction.Connection != null &&
                transaction.Connection.State == ConnectionState.Open)
                try
                {
                    CancelRequested = true;

                    var hasRollbacked = false;

                    if (ex != null && ex is DbCommandException dbe) hasRollbacked = dbe.HasRollbackedTransaction;

                    if (!hasRollbacked) transaction.Rollback();
                }
                catch (Exception e)
                {
                    //throw;
                }
        }

        private CommandInfo GetCommandInfo(string commandText, Dictionary<string, object> parameters = null,
            DbTransaction transaction = null)
        {
            var commandInfo = new CommandInfo
            {
                CommandType = CommandType.Text,
                CommandText = commandText,
                Parameters = parameters,
                Transaction = transaction,
                CancellationToken = CancellationTokenSource.Token
            };

            return commandInfo;
        }

        private BulkCopyInfo GetBulkCopyInfo(Table table, SchemaInfo schemaInfo, DbTransaction transaction = null)
        {
            var tableName = GetMappedTableName(table.Name);

            var bulkCopyInfo = new BulkCopyInfo
            {
                SourceDatabaseType = Source.DbInterpreter.DatabaseType,
                KeepIdentity = Target.DbInterpreter.Option.TableScriptsGenerateOption.GenerateIdentity,
                DestinationTableName = tableName,
                Transaction = transaction,
                CancellationToken = CancellationTokenSource.Token
            };

            var mappedSchema = SchemaInfoHelper.GetMappedSchema(table.Schema, Option.SchemaMappings);

            if (mappedSchema == null) mappedSchema = Target.DbInterpreter.DefaultSchema;

            bulkCopyInfo.DestinationTableSchema = mappedSchema;
            bulkCopyInfo.Columns =
                schemaInfo.TableColumns.Where(item => item.TableName == tableName && item.Schema == mappedSchema);

            return bulkCopyInfo;
        }

        private string GetMappedTableName(string tableName)
        {
            return SchemaInfoHelper.GetMappedTableName(tableName, Source.TableNameMappings);
        }

        private DbConvertResult HandleError(ConvertException ex)
        {
            HasError = true;
            IsBusy = false;

            var errMsg = ExceptionHelper.GetExceptionDetails(ex);
            Feedback(this, errMsg, FeedbackInfoType.Error);

            var result = new DbConvertResult();
            result.InfoType = DbConvertResultInfoType.Error;
            result.Message = errMsg;

            return result;
        }

        public void Cancle()
        {
            CancelRequested = true;

            if (Source != null) Source.DbInterpreter.CancelRequested = true;

            if (Target != null) Target.DbInterpreter.CancelRequested = true;

            Rollback();

            if (CancellationTokenSource != null) CancellationTokenSource.Cancel();
        }

        private (Table Table, List<TableColumn> Columns) GetTargetTableColumns(SchemaInfo targetSchemaInfo,
            string targetSchema, Table sourceTable, List<TableColumn> sourceColumns)
        {
            var mappedTableName = GetMappedTableName(sourceTable.Name);

            var targetTable = targetSchemaInfo.Tables.FirstOrDefault(item =>
                (item.Schema == targetSchema || string.IsNullOrEmpty(targetSchema)) && item.Name == mappedTableName);

            if (targetTable == null)
            {
                Feedback(this, $"Source table {sourceTable.Name} cannot get a target table.", FeedbackInfoType.Error);
                return (null, null);
            }

            var targetTableColumns = new List<TableColumn>();

            foreach (var sourceColumn in sourceColumns)
            {
                var targetTableColumn = targetSchemaInfo.TableColumns.FirstOrDefault(item =>
                    (item.Schema == targetSchema || string.IsNullOrEmpty(targetSchema)) &&
                    item.TableName == mappedTableName && item.Name == sourceColumn.Name);

                if (targetTableColumn == null)
                {
                    Feedback(this,
                        $"Source column {sourceColumn.TableName} of table {sourceColumn.TableName} cannot get a target column.",
                        FeedbackInfoType.Error);
                    return (null, null);
                }

                targetTableColumns.Add(targetTableColumn);
            }

            return (targetTable, targetTableColumns);
        }

        private void ConvertSchema(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            SchemaInfo targetSchemaInfo)
        {
            var schemaMappings = Option.SchemaMappings;

            if (schemaMappings.Count == 0)
            {
                schemaMappings.Add(new SchemaMappingInfo
                    { SourceSchema = "", TargetSchema = targetInterpreter.DefaultSchema });
            }
            else
            {
                if (sourceInterpreter.DefaultSchema != null && targetInterpreter.DefaultSchema != null)
                    if (!schemaMappings.Any(item => item.SourceSchema == sourceInterpreter.DefaultSchema))
                        schemaMappings.Add(new SchemaMappingInfo
                        {
                            SourceSchema = sourceInterpreter.DefaultSchema,
                            TargetSchema = targetInterpreter.DefaultSchema
                        });
            }

            SchemaInfoHelper.MapDatabaseObjectSchema(targetSchemaInfo, schemaMappings);
        }

        public void Feedback(object owner, string content, FeedbackInfoType infoType = FeedbackInfoType.Info,
            bool enableLog = true)
        {
            if (infoType == FeedbackInfoType.Error) HasError = true;

            var info = new FeedbackInfo
                { InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(content), Owner = owner };

            FeedbackHelper.Feedback(observer, info, enableLog);

            if (OnFeedback != null) OnFeedback(info);
        }
    }
}