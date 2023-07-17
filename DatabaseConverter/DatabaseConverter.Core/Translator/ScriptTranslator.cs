using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Core;
using SqlAnalyser.Model;

namespace DatabaseConverter.Core
{
    public class ScriptTranslator<T> : DbObjectTokenTranslator
        where T : ScriptDbObject
    {
        private ScriptBuildFactory scriptBuildFactory;
        private readonly IEnumerable<T> scripts;


        public ScriptTranslator(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter,
            IEnumerable<T> scripts) : base(sourceDbInterpreter, targetDbInterpreter)
        {
            this.scripts = scripts;
        }

        public bool AutoMakeupSchemaName { get; set; } = true;
        public bool IsCommonScript { get; set; }

        public override void Translate()
        {
            if (sourceDbInterpreter.DatabaseType == targetDbInterpreter.DatabaseType) return;

            if (hasError) return;

            LoadMappings();

            scriptBuildFactory = GetScriptBuildFactory();

            var total = scripts.Count();
            var count = 0;
            var successCount = 0;

            foreach (var dbObj in scripts)
            {
                if (hasError) break;

                if (string.IsNullOrEmpty(dbObj.Definition)) continue;

                count++;

                var type = typeof(T);

                var percent = total == 1 ? "" : $"({count}/{total})";

                FeedbackInfo($"Begin to translate {type.Name}: \"{dbObj.Name}\"{percent}.");

                var result = TranslateScript(dbObj);

                if (Option.CollectTranslateResultAfterTranslated) TranslateResults.Add(result);

                if (!result.HasError) successCount++;
            }

            if (total > 1)
                FeedbackInfo($"Translated {total} script(s): {successCount} succeeded, {total - successCount} failed.");
        }

        private TranslateResult TranslateScript(ScriptDbObject dbObj, bool isPartial = false)
        {
            var translateResult = new TranslateResult
            {
                DbObjectType = DbObjectHelper.GetDatabaseObjectType(dbObj), DbObjectSchema = dbObj.Schema,
                DbObjectName = dbObj.Name
            };

            try
            {
                var type = typeof(T);

                var tokenProcessed = false;

                dbObj.Definition = dbObj.Definition.Trim();

                if (Option.RemoveCarriagRreturnChar) dbObj.Definition = dbObj.Definition.Replace("\r\n", "\n");

                if (sourceDbType == DatabaseType.MySql)
                {
                    var dbName = sourceDbInterpreter.ConnectionInfo.Database;

                    if (dbName != null)
                        dbObj.Definition = dbObj.Definition.Replace($"`{dbName}`.", "").Replace($"{dbName}.", "");
                }
                else if (sourceDbType == DatabaseType.Postgres)
                {
                    if (dbObj.Definition.Contains("::"))
                    {
                        var dataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(sourceDbType);

                        dbObj.Definition = TranslateHelper.RemovePostgresDataTypeConvertExpression(dbObj.Definition,
                            dataTypeSpecs, targetDbInterpreter.QuotationLeftChar,
                            targetDbInterpreter.QuotationRightChar);
                    }
                }

                var originalDefinition = dbObj.Definition;
                AnalyseResult analyseResult;

                var sqlAnalyser = GetSqlAnalyser(sourceDbInterpreter.DatabaseType, originalDefinition);

                if (!isPartial)
                    analyseResult = sqlAnalyser.Analyse<T>();
                else
                    analyseResult = sqlAnalyser.AnalyseCommon();

                var script = analyseResult.Script;

                if (script == null)
                {
                    translateResult.Error = analyseResult.Error;
                    translateResult.Data = dbObj.Definition;

                    return translateResult;
                }

                var replaced = false;

                if (analyseResult.HasError)
                {
                    #region Special handle for view

                    if (typeof(T) == typeof(View))
                    {
                        //Currently, ANTLR can't parse some complex tsql accurately, so it uses general strategy.
                        if (sourceDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                        {
                            var viewTranslator =
                                new ViewTranslator(sourceDbInterpreter, targetDbInterpreter,
                                        new List<View> { dbObj as View })
                                    { ContinueWhenErrorOccurs = ContinueWhenErrorOccurs };
                            viewTranslator.Translate();

                            replaced = true;
                        }

                        //Currently, ANTLR can't parse some view correctly, use procedure to parse it temporarily.
                        if (sourceDbInterpreter.DatabaseType == DatabaseType.Oracle)
                        {
                            var oldDefinition = dbObj.Definition;

                            var asIndex = oldDefinition.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);

                            var sbNewDefinition = new StringBuilder();

                            sbNewDefinition.AppendLine($"CREATE OR REPLACE PROCEDURE {dbObj.Name} AS");
                            sbNewDefinition.AppendLine("BEGIN");
                            sbNewDefinition.AppendLine($"{oldDefinition.Substring(asIndex + 5).TrimEnd(';') + ";"}");
                            sbNewDefinition.AppendLine($"END {dbObj.Name};");

                            dbObj.Definition = sbNewDefinition.ToString();

                            sqlAnalyser = GetSqlAnalyser(sourceDbType, dbObj.Definition);

                            var procResult = sqlAnalyser.Analyse<Procedure>();

                            if (!procResult.HasError)
                            {
                                ProcessTokens(dbObj, procResult.Script);

                                tokenProcessed = true;

                                dbObj.Definition = Regex.Replace(dbObj.Definition, " PROCEDURE ", " VIEW ",
                                    RegexOptions.IgnoreCase);
                                dbObj.Definition = Regex.Replace(dbObj.Definition, @"(BEGIN[\r][\n])|(END[\r][\n])", "",
                                    RegexOptions.IgnoreCase);

                                replaced = true;
                            }
                        }
                    }

                    #endregion
                }

                if (!analyseResult.HasError && !tokenProcessed)
                {
                    if (string.IsNullOrEmpty(dbObj.Name) && !string.IsNullOrEmpty(analyseResult.Script?.Name?.Symbol))
                    {
                        if (AutoMakeupSchemaName) dbObj.Schema = analyseResult.Script.Schema;

                        TranslateHelper.RestoreTokenValue(originalDefinition, analyseResult.Script.Name);

                        dbObj.Name = analyseResult.Script.Name.Symbol;
                    }

                    ProcessTokens(dbObj, script);
                }

                dbObj.Definition = ReplaceVariables(dbObj.Definition, variableMappings);

                if (script is TriggerScript)
                {
                    var triggerVariableMappings = TriggerVariableMappingManager.GetVariableMappings();

                    dbObj.Definition = ReplaceVariables(dbObj.Definition, triggerVariableMappings);
                }

                if (script is RoutineScript)
                    if (sourceDbType == DatabaseType.Postgres)
                        if (!isPartial)
                        {
                            var declaresAndBody =
                                PostgresTranslateHelper.ExtractRountineScriptDeclaresAndBody(originalDefinition);

                            var scriptDbObject = new ScriptDbObject { Definition = declaresAndBody };

                            var res = TranslateScript(scriptDbObject, true);

                            if (!res.HasError)
                                dbObj.Definition =
                                    PostgresTranslateHelper.MergeDefinition(dbObj.Definition,
                                        scriptDbObject.Definition);
                            else
                                analyseResult = new AnalyseResult { Error = res.Error as SqlSyntaxError };
                        }

                dbObj.Definition =
                    TranslateHelper.TranslateComments(sourceDbInterpreter, targetDbInterpreter, dbObj.Definition);

                if (isPartial) return translateResult;

                translateResult.Error = replaced ? null : analyseResult.Error;
                translateResult.Data = dbObj.Definition;

                FeedbackInfo(
                    $"End translate {type.Name}: \"{dbObj.Name}\", translate result: {(analyseResult.HasError ? "Error" : "OK")}.");

                if (!replaced && analyseResult.HasError)
                {
                    var errMsg = ParseSqlSyntaxError(analyseResult.Error, originalDefinition).ToString();

                    FeedbackError(errMsg, ContinueWhenErrorOccurs);

                    if (!ContinueWhenErrorOccurs) hasError = true;
                }

                return translateResult;
            }
            catch (Exception ex)
            {
                var sce = new ScriptConvertException<T>(ex)
                {
                    SourceServer = sourceDbInterpreter.ConnectionInfo.Server,
                    SourceDatabase = sourceDbInterpreter.ConnectionInfo.Database,
                    SourceObject = dbObj.Name,
                    TargetServer = targetDbInterpreter.ConnectionInfo.Server,
                    TargetDatabase = targetDbInterpreter.ConnectionInfo.Database,
                    TargetObject = dbObj.Name
                };

                if (!ContinueWhenErrorOccurs)
                {
                    hasError = true;
                    throw sce;
                }

                FeedbackError(ExceptionHelper.GetExceptionDetails(ex), ContinueWhenErrorOccurs);

                return translateResult;
            }
        }

        private void ProcessTokens(ScriptDbObject dbObj, CommonScript script)
        {
            if (typeof(T) == typeof(Function))
            {
                var sqlAnalyser = GetSqlAnalyser(sourceDbType, dbObj.Definition);

                var result = sqlAnalyser.AnalyseFunction();

                if (!result.HasError)
                {
                    var routine = result.Script as RoutineScript;

                    if (targetDbInterpreter.DatabaseType == DatabaseType.MySql && routine.ReturnTable != null)
                        routine.Type = RoutineType.PROCEDURE;
                }
            }

            using (var tokenProcessor =
                   new ScriptTokenProcessor(script, dbObj, sourceDbInterpreter, targetDbInterpreter))
            {
                tokenProcessor.UserDefinedTypes = UserDefinedTypes;
                tokenProcessor.Option = Option;

                tokenProcessor.Process();

                string anotherDefinition = null;

                if (typeof(T) == typeof(TableTrigger))
                    //make up a trigger function
                    if (targetDbType == DatabaseType.Postgres)
                    {
                        var name = targetDbInterpreter.GetQuotedString($"func_{dbObj.Name}");
                        var nameWithSchema = string.IsNullOrEmpty(script.Schema)
                            ? targetDbInterpreter.GetQuotedString(name)
                            : $"{script.Schema}.{name}";

                        var triggerFunctionName = new NameToken(nameWithSchema);

                        var rs = new RoutineScript
                        {
                            Name = triggerFunctionName, Type = RoutineType.FUNCTION,
                            ReturnDataType = new TokenInfo("trigger")
                        };
                        rs.Statements.AddRange(script.Statements);

                        (script as TriggerScript).FunctionName = triggerFunctionName;

                        anotherDefinition = StringHelper.FormatScript(scriptBuildFactory.GenerateScripts(rs).Script);
                    }

                var scriptBuildResult = scriptBuildFactory.GenerateScripts(script);

                dbObj.Definition = StringHelper.FormatScript(scriptBuildResult.Script);

                if (anotherDefinition != null)
                    dbObj.Definition = anotherDefinition + Environment.NewLine + dbObj.Definition;
            }
        }

        private SqlSyntaxError ParseSqlSyntaxError(SqlSyntaxError error, string definition)
        {
            foreach (var item in error.Items)
                item.Text = definition.Substring(item.StartIndex, item.StopIndex - item.StartIndex + 1);

            return error;
        }

        public SqlAnalyserBase GetSqlAnalyser(DatabaseType dbType, string content)
        {
            var sqlAnalyser = TranslateHelper.GetSqlAnalyser(dbType, content);

            sqlAnalyser.RuleAnalyser.Option.ParseTokenChildren = true;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctions = true;
            sqlAnalyser.RuleAnalyser.Option.ExtractFunctionChildren = false;
            sqlAnalyser.RuleAnalyser.Option.IsCommonScript = IsCommonScript;

            return sqlAnalyser;
        }

        public ScriptBuildFactory GetScriptBuildFactory()
        {
            var factory = TranslateHelper.GetScriptBuildFactory(targetDbType);

            factory.ScriptBuilderOption.OutputRemindInformation = Option.OutputRemindInformation;

            return factory;
        }
    }
}