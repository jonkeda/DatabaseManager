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

namespace Databases.Handlers.MySql
{
    public class MySqlScriptBuildFactory : ScriptBuildFactory
    {
        public override DatabaseType DatabaseType => DatabaseType.MySql;

        protected override void PreHandleStatements(List<Statement> statements)
        {
            PreHandle(statements);
        }

        protected override void PostHandleStatements(StringBuilder sb)
        {
            base.PostHandleStatements(sb);

            PostHandle(sb);
        }

        private void PreHandle(List<Statement> statements)
        {
            StatementBuilder.Option.CollectDeclareStatement = true;
            StatementBuilder.Option.NotBuildDeclareStatement = true;
            StatementBuilder.Option.CollectDeclareStatement = true;

            base.PreHandleStatements(statements);

            MySqlAnalyserHelper.RearrangeStatements(statements);
        }

        private void PostHandle(StringBuilder sb, RoutineType routineType = RoutineType.UNKNOWN,
            ScriptBuildResult result = null, int? beginIndex = default)
        {
            var declareStatements = StatementBuilder.GetDeclareStatements();

            if (declareStatements.Count > 0)
            {
                StatementBuilder.Clear();

                var sbDeclare = new StringBuilder();

                var hasDeclareCursor = false;
                var hasDeclareCursorHanlder = false;

                foreach (var declareStatement in declareStatements)
                {
                    if (declareStatement is DeclareCursorStatement)
                    {
                        hasDeclareCursor = true;
                    }

                    if (declareStatement is DeclareCursorHandlerStatement)
                    {
                        hasDeclareCursorHanlder = true;
                    }
                }

                if (hasDeclareCursor && !hasDeclareCursorHanlder)
                {
                    var declareVaribleStatement = new DeclareVariableStatement
                    {
                        Name = new TokenInfo("FINISHED") { Type = TokenType.VariableName },
                        DataType = new TokenInfo("INT"),
                        DefaultValue = new TokenInfo("0")
                    };

                    StatementBuilder.DeclareVariableStatements.Insert(0, declareVaribleStatement);

                    var declareHandlerStatement = new DeclareCursorHandlerStatement();
                    declareHandlerStatement.Statements.Add(new SetStatement
                    {
                        Key = new TokenInfo("FINISHED") { Type = TokenType.VariableName }, Value = new TokenInfo("1")
                    });

                    StatementBuilder.OtherDeclareStatements.Add(declareHandlerStatement);
                }

                foreach (var declareStatement in declareStatements)
                {
                    StatementBuilder.Option.NotBuildDeclareStatement = false;
                    StatementBuilder.Option.CollectDeclareStatement = false;

                    var content = BuildStatement(declareStatement, routineType).TrimEnd();

                    sbDeclare.AppendLine(content);
                }

                sb.Insert(result == null ? 0 : result.BodyStartIndex, sbDeclare.ToString());

                if (result != null)
                {
                    result.BodyStartIndex += sbDeclare.Length;
                    result.BodyStopIndex += sbDeclare.Length;
                }
            }

            if (StatementBuilder.SpecialStatements.Any(item => item.GetType() == typeof(LeaveStatement)))
            {
                StatementBuilder.Option.CollectSpecialStatementTypes.Clear();
                StatementBuilder.SpecialStatements.Clear();

                if (beginIndex.HasValue)
                {
                    sb.Insert(beginIndex.Value, "sp:");
                }

                if (result != null)
                {
                    result.BodyStartIndex += 3;
                    result.BodyStopIndex += 3;
                }
            }
        }

        public override ScriptBuildResult GenerateRoutineScripts(RoutineScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE {script.Type.ToString()} {script.NameWithSchema}");

            sb.AppendLine("(");

            if (script.Parameters.Count > 0)
            {
                var i = 0;

                foreach (var parameter in script.Parameters)
                {
                    var parameterType = parameter.ParameterType;

                    var strParameterType = "";

                    if (parameterType.HasFlag(ParameterType.IN) && parameterType.HasFlag(ParameterType.OUT))
                    {
                        strParameterType = "INOUT";
                    }
                    else if (parameterType != ParameterType.NONE)
                    {
                        strParameterType = parameterType.ToString();
                    }

                    sb.AppendLine(
                        $"{strParameterType} {parameter.Name} {parameter.DataType}{(i == script.Parameters.Count - 1 ? "" : ",")}");

                    i++;
                }
            }

            sb.AppendLine(")");

            if (script.Type == RoutineType.FUNCTION)
            {
                var returnType = script.ReturnDataType != null ? script.ReturnDataType.Symbol
                    : script.ReturnTable != null ? $"#Table#{script.ReturnTable.Name}" : "";

                sb.AppendLine($"RETURNS {returnType}");
            }

            var beginIndex = sb.Length - 1;

            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            if (script.ReturnTable != null)
            {
                sb.AppendLine((StatementBuilder as MySqlStatementScriptBuilder).BuildTable(script.ReturnTable));
            }

            StatementBuilder.Option.CollectSpecialStatementTypes.Add(typeof(LeaveStatement));

            PreHandle(script.Statements);

            foreach (var statement in script.Statements)
            {
                sb.AppendLine(BuildStatement(statement, script.Type));
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END");

            PostHandle(sb, script.Type, result, beginIndex);

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

            //only allow one event type: INSERT, UPDATE OR DELETE
            var events = script.Events.FirstOrDefault(); // string.Join(",", script.Events);

            var time = script.Time == TriggerTime.INSTEAD_OF ? "AFTER" : script.Time.ToString();

            sb.AppendLine($"CREATE TRIGGER {script.NameWithSchema} {time} {events} ON {script.TableName}");
            sb.AppendLine($"FOR EACH ROW {script.Behavior} {script.OtherTriggerName}");

            var beginIndex = sb.Length - 1;
            var hasLeaveStatement = false;

            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements.Where(item => item is DeclareVariableStatement))
            {
                sb.AppendLine(BuildStatement(statement));
            }

            foreach (var statement in script.Statements.Where(item => !(item is DeclareVariableStatement)))
            {
                if (statement is LeaveStatement)
                {
                    hasLeaveStatement = true;
                }

                sb.AppendLine(BuildStatement(statement, RoutineType.TRIGGER));
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END");

            if (hasLeaveStatement)
            {
                sb.Insert(beginIndex, "sp:");
            }

            result.Script = sb.ToString();

            return result;
        }
    }
}