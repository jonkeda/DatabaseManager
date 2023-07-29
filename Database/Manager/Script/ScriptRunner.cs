using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseConverter.Core;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Model;
using Databases.Exceptions;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace DatabaseManager.Core
{
    public class ScriptRunner
    {
        private IObserver<FeedbackInfo> observer;
        private DbTransaction transaction;

        public ScriptRunner()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        public CancellationTokenSource CancellationTokenSource { get; }
        public bool CancelRequested { get; private set; }

        public bool IsBusy { get; private set; }

        public int LimitCount { get; set; } = 1000;

        public event FeedbackHandler OnFeedback;

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public async Task<QueryResult> Run(DatabaseType dbType, ConnectionInfo connectionInfo, string script,
            ScriptAction action = ScriptAction.NONE, List<RoutineParameter> parameters = null)
        {
            CancelRequested = false;
            IsBusy = false;

            var result = new QueryResult();

            var option = new DbInterpreterOption { RequireInfoMessage = true };

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);

            dbInterpreter.Subscribe(observer);

            try
            {
                var scriptParser = new ScriptParser(dbInterpreter, script);

                var cleanScript = scriptParser.CleanScript;

                if (string.IsNullOrEmpty(cleanScript))
                {
                    result.DoNothing = true;
                    return result;
                }

                using (var dbConnection = dbInterpreter.CreateConnection())
                {
                    if (scriptParser.IsSelect())
                    {
                        IsBusy = true;
                        result.ResultType = QueryResultType.Grid;

                        script = DecorateSelectWithLimit(dbInterpreter, script);

                        if (!scriptParser.IsCreateOrAlterScript() && dbInterpreter.ScriptsDelimiter.Length == 1)
                        {
                            cleanScript = script.Trim().TrimEnd(dbInterpreter.ScriptsDelimiter[0]);
                        }

                        var dataTable = await dbInterpreter.GetDataTableAsync(dbConnection, cleanScript);

                        result.Result = dataTable;
                    }
                    else
                    {
                        IsBusy = true;
                        result.ResultType = QueryResultType.Text;

                        await dbConnection.OpenAsync();

                        transaction = dbConnection.BeginTransaction();

                        var commands = Enumerable.Empty<string>();

                        if (scriptParser.IsCreateOrAlterScript())
                        {
                            if (dbInterpreter.DatabaseType == DatabaseType.Oracle)
                            {
                                var scriptType = ScriptParser.DetectScriptType(script, dbInterpreter);

                                if (scriptType != ScriptType.Procedure && scriptType != ScriptType.Function &&
                                    scriptType != ScriptType.Trigger)
                                {
                                    script = script.Trim().TrimEnd(dbInterpreter.ScriptsDelimiter[0]);
                                }
                            }

                            commands = new[] { script };
                        }
                        else
                        {
                            var delimiter = dbInterpreter.ScriptsDelimiter;

                            commands = script.Split(new[] { delimiter, delimiter.Replace("\r", "\n") },
                                StringSplitOptions.RemoveEmptyEntries);
                        }

                        var affectedRows = 0;

                        var isProcedureCall = action == ScriptAction.EXECUTE;

                        var commandType = isProcedureCall && dbInterpreter.DatabaseType == DatabaseType.Oracle
                            ? CommandType.StoredProcedure
                            : CommandType.Text;

                        foreach (var command in commands)
                        {
                            if (string.IsNullOrEmpty(command.Trim()))
                            {
                                continue;
                            }

                            var commandInfo = new CommandInfo
                            {
                                CommandType = commandType,
                                CommandText = command,
                                Transaction = transaction,
                                CancellationToken = CancellationTokenSource.Token
                            };

                            if (commandType == CommandType.StoredProcedure)
                            {
                                if (action == ScriptAction.EXECUTE && dbInterpreter.DatabaseType == DatabaseType.Oracle)
                                {
                                    ParseOracleProcedureCall(commandInfo, parameters);
                                }
                            }

                            var res = await dbInterpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);

                            affectedRows += res == -1 ? 0 : res;
                        }

                        result.Result = affectedRows;

                        if (!dbInterpreter.HasError && !CancelRequested)
                        {
                            transaction.Commit();
                        }
                    }

                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                Rollback(ex);

                result.ResultType = QueryResultType.Text;
                result.HasError = true;
                result.Result = ex.Message;

                HandleError(ex);
            }

            return result;
        }

        private void ParseOracleProcedureCall(CommandInfo cmd, List<RoutineParameter> parameters)
        {
            var sqlAnalyser = TranslateHelper.GetSqlAnalyser(DatabaseType.Oracle, cmd.CommandText);

            sqlAnalyser.RuleAnalyser.Option.ParseTokenChildren = false;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctions = false;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctionChildren = false;
            sqlAnalyser.RuleAnalyser.Option.IsCommonScript = true;

            var result = sqlAnalyser.AnalyseCommon();

            if (result != null && !result.HasError)
            {
                var cs = result.Script;

                var callStatement = cs.Statements.FirstOrDefault(item => item is CallStatement) as CallStatement;

                if (callStatement != null)
                {
                    cmd.CommandText = callStatement.Name.Symbol;

                    if (parameters != null && callStatement.Parameters != null &&
                        parameters.Count == callStatement.Parameters.Count)
                    {
                        cmd.Parameters = new Dictionary<string, object>();

                        var i = 0;

                        foreach (var para in parameters.OrderBy(item => item.Order))
                        {
                            cmd.Parameters.Add(para.Name, callStatement.Parameters[i]?.Value?.Symbol);

                            i++;
                        }
                    }
                }
            }
        }

        public async Task Run(DbInterpreter dbInterpreter, IEnumerable<Script> scripts)
        {
            using (var dbConnection = dbInterpreter.CreateConnection())
            {
                await dbConnection.OpenAsync();

                var transaction = dbConnection.BeginTransaction();

                Func<Script, bool> isValidScript = s =>
                {
                    return !(s is NewLineSript || s is SpliterScript || string.IsNullOrEmpty(s.Content) ||
                             s.Content == dbInterpreter.ScriptsDelimiter);
                };

                var count = scripts.Where(item => isValidScript(item)).Count();
                var i = 0;

                foreach (var s in scripts)
                {
                    if (!isValidScript(s))
                    {
                        continue;
                    }

                    var sql = s.Content?.Trim();

                    if (!string.IsNullOrEmpty(sql) && sql != dbInterpreter.ScriptsDelimiter)
                    {
                        i++;

                        if (dbInterpreter.ScriptsDelimiter.Length == 1 && sql.EndsWith(dbInterpreter.ScriptsDelimiter))
                        {
                            sql = sql.TrimEnd(dbInterpreter.ScriptsDelimiter.ToArray());
                        }

                        if (!dbInterpreter.HasError)
                        {
                            var commandInfo = new CommandInfo
                            {
                                CommandType = CommandType.Text,
                                CommandText = sql,
                                Transaction = transaction,
                                CancellationToken = CancellationTokenSource.Token
                            };

                            await dbInterpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
                        }
                    }
                }

                transaction.Commit();
            }
        }

        private void HandleError(Exception ex)
        {
            IsBusy = false;

            var errMsg = ExceptionHelper.GetExceptionDetails(ex);
            Feedback(this, errMsg, FeedbackInfoType.Error, true, true);
        }

        public void Cancel()
        {
            CancelRequested = true;

            Rollback();

            CancellationTokenSource?.Cancel();
        }

        private void Rollback(Exception ex = null)
        {
            if (transaction != null
                && transaction.Connection != null
                && transaction.Connection.State == ConnectionState.Open)
            {
                try
                {
                    CancelRequested = true;

                    var hasRolledBack = false;

                    if (ex != null && ex is DbCommandException dbe)
                    {
                        hasRolledBack = dbe.HasRolledBackTransaction;
                    }

                    if (!hasRolledBack)
                    {
                        transaction.Rollback();
                    }
                }
                catch
                {
                    //throw;
                }
            }
        }

        public string DecorateSelectWithLimit(DbInterpreter dbInterpreter, string script)
        {
            var databaseType = dbInterpreter.DatabaseType;

            var sqlAnalyser = TranslateHelper.GetSqlAnalyser(databaseType, script);

            sqlAnalyser.RuleAnalyser.Option.ParseTokenChildren = false;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctions = false;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctionChildren = false;
            sqlAnalyser.RuleAnalyser.Option.IsCommonScript = true;

            var result = sqlAnalyser.AnalyseCommon();

            if (result != null && !result.HasError)
            {
                var cs = result.Script;

                var selectStatement = cs.Statements.FirstOrDefault(item => item is SelectStatement) as SelectStatement;

                if (selectStatement != null)
                {
                    var tableName = selectStatement.TableName;

                    if (tableName == null)
                    {
                        if (selectStatement.HasFromItems)
                        {
                            tableName = selectStatement.FromItems[0].TableName;
                        }
                    }

                    var hasTableName = tableName != null && tableName.Symbol?.ToUpper() != "DUAL";

                    if (hasTableName && selectStatement.TopInfo == null && selectStatement.LimitInfo == null)
                    {
                        var defaultOrder = dbInterpreter.GetDefaultOrder();

                        if (selectStatement.OrderBy == null && !string.IsNullOrEmpty(defaultOrder))
                        {
                            selectStatement.OrderBy = new List<TokenInfo> { new TokenInfo(defaultOrder) };
                        }

                        if (databaseType == DatabaseType.SqlServer)
                        {
                            selectStatement.TopInfo = new SelectTopInfo
                                { TopCount = new TokenInfo(LimitCount.ToString()) };
                        }
                        else if (databaseType == DatabaseType.MySql || databaseType == DatabaseType.Postgres)
                        {
                            selectStatement.LimitInfo = new SelectLimitInfo
                                { StartRowIndex = new TokenInfo("0"), RowCount = new TokenInfo(LimitCount.ToString()) };
                        }

                        var scriptBuildFactory = TranslateHelper.GetScriptBuildFactory(databaseType);

                        script = scriptBuildFactory.GenerateScripts(cs).Script;

                        if (databaseType == DatabaseType.Oracle) //oracle low version doesn't support limit clause.
                        {
                            script = $@"SELECT * FROM
                               (
                                 {script.Trim().TrimEnd(';')}
                               ) TEMP
                               WHERE ROWNUM BETWEEN 1 AND {LimitCount}";
                        }
                    }
                }
            }

            return script;
        }

        public void Feedback(object owner, string content, FeedbackInfoType infoType = FeedbackInfoType.Info,
            bool enableLog = true, bool suppressError = false)
        {
            var info = new FeedbackInfo
                { InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(content), Owner = owner };

            FeedbackHelper.Feedback(suppressError ? null : observer, info, enableLog);

            OnFeedback?.Invoke(info);
        }
    }
}