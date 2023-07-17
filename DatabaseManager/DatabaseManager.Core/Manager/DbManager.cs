using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class DbManager
    {
        private readonly DbInterpreter dbInterpreter;
        private IObserver<FeedbackInfo> observer;
        private readonly DbScriptGenerator scriptGenerator;

        public DbManager()
        {
        }

        public DbManager(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
            scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(dbInterpreter);
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public async Task ClearData(List<Table> tables = null)
        {
            FeedbackInfo("Begin to clear data...");

            if (tables == null) tables = await dbInterpreter.GetTablesAsync();

            var failed = false;

            DbTransaction transaction = null;

            try
            {
                FeedbackInfo("Disable constrains.");

                var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(dbInterpreter);

                using (var dbConnection = dbInterpreter.CreateConnection())
                {
                    await dbConnection.OpenAsync();

                    await SetConstrainsEnabled(dbConnection, false);

                    transaction = await dbConnection.BeginTransactionAsync();

                    foreach (var table in tables)
                    {
                        var sql = $"DELETE FROM {dbInterpreter.GetQuotedDbObjectNameWithSchema(table)}";

                        FeedbackInfo(sql);

                        var commandInfo = new CommandInfo
                        {
                            CommandType = CommandType.Text,
                            CommandText = sql,
                            Transaction = transaction
                        };

                        await dbInterpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
                    }

                    if (!dbInterpreter.HasError) transaction.Commit();

                    await SetConstrainsEnabled(dbConnection, true);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                FeedbackError(ExceptionHelper.GetExceptionDetails(ex));

                if (transaction != null)
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception iex)
                    {
                        LogHelper.LogError(iex.Message);
                    }
            }
            finally
            {
                if (failed)
                {
                    FeedbackInfo("Enable constrains.");

                    await SetConstrainsEnabled(null, true);
                }
            }

            FeedbackInfo("End clear data.");
        }

        private async Task SetConstrainsEnabled(DbConnection dbConnection, bool enabled)
        {
            var needDispose = false;

            if (dbConnection == null)
            {
                needDispose = true;
                dbConnection = dbInterpreter.CreateConnection();
            }

            var scripts = scriptGenerator.SetConstrainsEnabled(enabled);

            foreach (var script in scripts) await dbInterpreter.ExecuteNonQueryAsync(dbConnection, script.Content);

            if (needDispose)
            {
                using (dbConnection)
                {
                }

                ;
            }
        }

        public async Task EmptyDatabase(DatabaseObjectType databaseObjectType)
        {
            var sortObjectsByReference = dbInterpreter.Option.SortObjectsByReference;
            var fetchMode = dbInterpreter.Option.ObjectFetchMode;

            dbInterpreter.Option.SortObjectsByReference = true;
            dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

            FeedbackInfo("Begin to empty database...");

            var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(new SchemaInfoFilter
                { DatabaseObjectType = databaseObjectType });

            try
            {
                using (var connection = dbInterpreter.CreateConnection())
                {
                    await DropDbObjects(connection, schemaInfo.TableTriggers);
                    await DropDbObjects(connection, schemaInfo.Procedures);
                    await DropDbObjects(connection, schemaInfo.Views);
                    await DropDbObjects(connection, schemaInfo.TableTriggers);
                    await DropDbObjects(connection, schemaInfo.TableForeignKeys);
                    await DropDbObjects(connection, schemaInfo.Tables);
                    await DropDbObjects(connection, schemaInfo.Functions);
                    await DropDbObjects(connection, schemaInfo.UserDefinedTypes);
                    await DropDbObjects(connection, schemaInfo.Sequences);
                }
            }
            catch (Exception ex)
            {
                FeedbackError(ExceptionHelper.GetExceptionDetails(ex));
            }
            finally
            {
                dbInterpreter.Option.SortObjectsByReference = sortObjectsByReference;
                dbInterpreter.Option.ObjectFetchMode = fetchMode;
            }

            FeedbackInfo("End empty database.");
        }

        private async Task DropDbObjects<T>(DbConnection connection, List<T> dbObjects) where T : DatabaseObject
        {
            var names = new List<string>();

            foreach (var obj in dbObjects)
                if (!names.Contains(obj.Name))
                    try
                    {
                        await DropDbObject(obj, connection, true);

                        names.Add(obj.Name);
                    }
                    catch (Exception ex)
                    {
                        FeedbackError($@"Error occurs when drop ""{obj.Name}"":{ex.Message}", true);
                    }
        }

        private async Task<bool> DropDbObject(DatabaseObject dbObject, DbConnection connection = null,
            bool continueWhenErrorOccurs = false)
        {
            var typeName = dbObject.GetType().Name;

            FeedbackInfo($"Drop {typeName} \"{dbObject.Name}\".");

            var script = scriptGenerator.Drop(dbObject);

            if (script != null && !string.IsNullOrEmpty(script.Content))
            {
                var sql = script.Content;

                if (dbInterpreter.ScriptsDelimiter.Length == 1)
                    sql = sql.TrimEnd(dbInterpreter.ScriptsDelimiter.ToCharArray());

                var commandInfo = new CommandInfo
                    { CommandText = sql, ContinueWhenErrorOccurs = continueWhenErrorOccurs };

                if (connection != null)
                    await dbInterpreter.ExecuteNonQueryAsync(connection, commandInfo);
                else
                    await dbInterpreter.ExecuteNonQueryAsync(commandInfo);

                return true;
            }

            return false;
        }

        public async Task<bool> DropDbObject(DatabaseObject dbObject)
        {
            try
            {
                return await DropDbObject(dbObject, null);
            }
            catch (Exception ex)
            {
                FeedbackError(ExceptionHelper.GetExceptionDetails(ex));

                return false;
            }
        }

        public bool Backup(BackupSetting setting, ConnectionInfo connectionInfo)
        {
            try
            {
                var backup = DbBackup.GetInstance(ManagerUtil.GetDatabaseType(setting.DatabaseType));
                backup.Setting = setting;
                backup.ConnectionInfo = connectionInfo;

                FeedbackInfo("Begin to backup...");

                var saveFilePath = backup.Backup();

                if (File.Exists(saveFilePath))
                    FeedbackInfo($"Database has been backuped to {saveFilePath}.");
                else
                    FeedbackInfo("Database has been backuped.");

                return true;
            }
            catch (Exception ex)
            {
                FeedbackError(ExceptionHelper.GetExceptionDetails(ex));
            }

            return false;
        }

        public async Task<TableDiagnoseResult> DiagnoseTable(DatabaseType databaseType, ConnectionInfo connectionInfo,
            string schema, TableDiagnoseType diagnoseType)
        {
            var dbDiagnosis = DbDiagnosis.GetInstance(databaseType, connectionInfo);
            dbDiagnosis.Schema = schema;

            dbDiagnosis.OnFeedback += Feedback;

            var result = await dbDiagnosis.DiagnoseTable(diagnoseType);

            return result;
        }

        public async Task<List<ScriptDiagnoseResult>> DiagnoseScript(DatabaseType databaseType,
            ConnectionInfo connectionInfo, string schema, ScriptDiagnoseType diagnoseType)
        {
            var dbDiagnosis = DbDiagnosis.GetInstance(databaseType, connectionInfo);
            dbDiagnosis.Schema = schema;

            dbDiagnosis.OnFeedback += Feedback;

            var results = await dbDiagnosis.DiagnoseScript(diagnoseType);

            return results;
        }

        public void Feedback(FeedbackInfoType infoType, string message)
        {
            var info = new FeedbackInfo
                { Owner = this, InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(message) };

            Feedback(info);
        }

        public void Feedback(FeedbackInfo info)
        {
            if (observer != null) FeedbackHelper.Feedback(observer, info);
        }

        public void FeedbackInfo(string message)
        {
            Feedback(FeedbackInfoType.Info, message);
        }

        public void FeedbackError(string message, bool skipError = false)
        {
            Feedback(new FeedbackInfo
                { InfoType = FeedbackInfoType.Error, Message = message, IgnoreError = skipError });
        }
    }
}