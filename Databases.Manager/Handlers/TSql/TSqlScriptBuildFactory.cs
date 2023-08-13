using System.Collections.Generic;
using System.Linq;
using System.Text;
using Databases.Model.Enum;
using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Statement.Cursor;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.Handlers.TSql
{
    public class TSqlScriptBuildFactory : ScriptBuildFactory
    {
        public override DatabaseType DatabaseType => DatabaseType.SqlServer;

        public override ScriptBuildResult GenerateRoutineScripts(RoutineScript script)
        {
            var result = new ScriptBuildResult();

            StatementBuilder.Option.CollectSpecialStatementTypes.Add(typeof(PreparedStatement));

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE {script.Type.ToString()} {script.NameWithSchema}");

            if (script.Parameters.Count > 0)
            {
                sb.AppendLine("(");

                var i = 0;
                foreach (var parameter in script.Parameters)
                {
                    var parameterType = parameter.ParameterType;

                    var strParameterType = "";

                    if (parameterType == ParameterType.IN)
                    {
                        strParameterType = "";
                    }
                    else if (parameterType.HasFlag(ParameterType.IN) && parameterType.HasFlag(ParameterType.OUT))
                    {
                        strParameterType = "OUT";
                    }
                    else if (parameterType != ParameterType.NONE)
                    {
                        strParameterType = parameterType.ToString();
                    }

                    var defaultValue = parameter.DefaultValue == null ? "" : "=" + parameter.DefaultValue;

                    sb.AppendLine(
                        $"{parameter.Name} {parameter.DataType} {defaultValue} {strParameterType}{(i == script.Parameters.Count - 1 ? "" : ",")}");

                    i++;
                }

                sb.AppendLine(")");
            }
            else if (script.Type == RoutineType.FUNCTION)
            {
                sb.AppendLine("(");
                sb.AppendLine(")");
            }

            if (script.Type == RoutineType.FUNCTION)
            {
                if (script.ReturnTable == null)
                {
                    sb.AppendLine($"RETURNS {script.ReturnDataType}");
                }
                else
                {
                    sb.AppendLine(
                        $"RETURNS {script.ReturnTable.Name}({string.Join(",", script.ReturnTable.Columns.Select(t => $"{t.Name.Symbol} {t.DataType}"))})");
                }
            }

            sb.AppendLine("AS");

            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            void AppendStatements(IEnumerable<Statement> statements)
            {
                foreach (var statement in statements)
                {
                    if (statement is WhileStatement @while)
                    {
                        var fetchCursorStatement =
                            @while.Statements.FirstOrDefault(item => item is FetchCursorStatement) as
                                FetchCursorStatement;

                        if (fetchCursorStatement != null && !statements.Any(item => item is FetchCursorStatement))
                        {
                            var condition = @while.Condition?.Symbol;

                            if (condition == null)
                            {
                                @while.Condition = new TokenInfo("");
                            }

                            @while.Condition.Symbol = "@@FETCH_STATUS = 0";

                            if (condition != null)
                            {
                                @while.Condition.Symbol += " AND " + condition;
                            }

                            sb.AppendLine(BuildStatement(fetchCursorStatement));
                        }
                    }

                    sb.AppendLine(BuildStatement(statement));
                }
            }

            var exceptionStatement =
                (ExceptionStatement)script.Statements.FirstOrDefault(item => item is ExceptionStatement);

            if (exceptionStatement != null)
            {
                sb.AppendLine("BEGIN TRY");
                AppendStatements(script.Statements.Where(item => !(item is ExceptionStatement)));
                sb.AppendLine("END TRY");

                sb.AppendLine("BEGIN CATCH");

                foreach (var exceptionItem in exceptionStatement.Items)
                {
                    sb.AppendLine(
                        $"IF {exceptionItem.Name} = ERROR_PROCEDURE() OR {exceptionItem.Name} = ERROR_NUMBER()");
                    sb.AppendLine("BEGIN");

                    AppendStatements(exceptionItem.Statements);

                    sb.AppendLine("END");
                }

                sb.AppendLine("END CATCH");
            }
            else
            {
                AppendStatements(script.Statements);
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END");

            result.Script = sb.ToString();

            return result;
        }

        public override ScriptBuildResult GenerateViewScripts(ViewScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE VIEW {script.NameWithSchema} AS");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements)
            {
                sb.AppendLine(BuildStatement(statement));
            }

            result.BodyStopIndex = sb.Length - 1;

            result.Script = sb.ToString();

            return result;
        }

        public override ScriptBuildResult GenerateTriggerScripts(TriggerScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            var time = script.Time == TriggerTime.BEFORE || script.Time == TriggerTime.INSTEAD_OF
                ? "INSTEAD OF"
                : script.Time.ToString();
            var events = string.Join(",", script.Events);

            sb.AppendLine($"CREATE TRIGGER {script.NameWithSchema} ON {script.TableName}");
            sb.AppendLine($"{time} {events} NOT FOR REPLICATION ");

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements)
            {
                sb.Append(BuildStatement(statement));
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END");

            result.Script = sb.ToString();

            return result;
        }
    }
}