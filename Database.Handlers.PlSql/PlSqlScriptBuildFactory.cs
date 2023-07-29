using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Statement.Cursor;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class PlSqlScriptBuildFactory : ScriptBuildFactory
    {
        public override DatabaseType DatabaseType => DatabaseType.Oracle;

        protected override void PreHandleStatements(List<Statement> statements)
        {
            base.PreHandleStatements(statements);

            PreHandle(statements);
        }

        protected override void PostHandleStatements(StringBuilder sb)
        {
            base.PostHandleStatements(sb);

            PostHandle(sb);
        }

        private void PreHandle(List<Statement> statements)
        {
            StatementBuilder.Option.NotBuildDeclareStatement = true;
            StatementBuilder.Option.CollectDeclareStatement = true;
            StatementBuilder.Option.CollectSpecialStatementTypes.Add(typeof(PreparedStatement));
        }

        private void PostHandle(StringBuilder sb, RoutineType routineType = RoutineType.UNKNOWN,
            ScriptBuildResult result = null, int? declareStartIndex = default)
        {
            var declareStatements = StatementBuilder.GetDeclareStatements();

            if (declareStatements.Count > 0)
            {
                StatementBuilder.Clear();

                var sbDeclare = new StringBuilder();

                foreach (var declareStatement in declareStatements)
                {
                    StatementBuilder.Option.NotBuildDeclareStatement = false;
                    StatementBuilder.Option.CollectDeclareStatement = false;

                    var content = BuildStatement(declareStatement, routineType).Trim();

                    sbDeclare.AppendLine(content.Replace("DECLARE ", ""));
                }

                sb.Insert(declareStartIndex.HasValue ? declareStartIndex.Value : 0, sbDeclare.ToString());

                if (result != null)
                {
                    result.BodyStartIndex += sbDeclare.Length;
                    result.BodyStopIndex += sbDeclare.Length;
                }
            }
        }

        public override ScriptBuildResult GenerateRoutineScripts(RoutineScript script)
        {
            var result = new ScriptBuildResult();

            PreHandle(script.Statements);

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE OR REPLACE {script.Type.ToString()} {script.Name}");

            if (script.Parameters.Count > 0)
            {
                sb.AppendLine("(");

                var i = 0;

                foreach (var parameter in script.Parameters)
                {
                    var parameterType = parameter.ParameterType;

                    var dataType = parameter.DataType.Symbol;
                    var defaultValue = parameter.DefaultValue == null ? "" : $" DEFAULT {parameter.DefaultValue}";
                    var strParameterType = "";

                    var parenthesesIndex = dataType.IndexOf("(", StringComparison.Ordinal);

                    if (parenthesesIndex > 0) dataType = dataType.Substring(0, parenthesesIndex);

                    if (parameterType.HasFlag(ParameterType.IN) && parameterType.HasFlag(ParameterType.OUT))
                        strParameterType = "IN OUT";
                    else if (parameterType != ParameterType.NONE) strParameterType = parameterType.ToString();

                    sb.AppendLine(
                        $"{parameter.Name} {strParameterType} {dataType}{defaultValue}{(i == script.Parameters.Count - 1 ? "" : ",")}");

                    i++;
                }

                sb.AppendLine(")");
            }

            if (script.Type == RoutineType.FUNCTION)
            {
                if (script.ReturnDataType != null)
                {
                    var dataType = script.ReturnDataType.Symbol;

                    if (DataTypeHelper.IsCharType(dataType))
                    {
                        var dataTypeInfo = DataTypeHelper.GetDataTypeInfo(dataType);
                        dataType = dataTypeInfo.DataType;
                    }

                    sb.AppendLine($"RETURN {dataType}");
                }
                else if (script.ReturnTable != null)
                {
                    //sb.AppendLine($"RETURN {script.ReturnTable}");
                }
            }

            sb.AppendLine("AS");

            var declareStartIndex = sb.Length - 1;

            sb.AppendLine("BEGIN");

            if (script.ReturnTable != null)
            {
            }

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements)
            {
                if (statement is WhileStatement @while)
                {
                    var fs =
                        @while.Statements.FirstOrDefault(item => item is FetchCursorStatement) as FetchCursorStatement;

                    if (fs != null)
                    {
                        @while.Condition.Symbol = "1=1";

                        @while.Statements.Insert(0,
                            new LoopExitStatement { Condition = new TokenInfo($"{fs.CursorName}%NOTFOUND") });
                    }
                }

                sb.AppendLine(BuildStatement(statement, script.Type));
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine($"END {script.Name};");

            PostHandle(sb, script.Type, result, declareStartIndex);

            result.Script = sb.ToString();

            return result;
        }

        public override ScriptBuildResult GenerateViewScripts(ViewScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE OR REPLACE VIEW {script.NameWithSchema} AS");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements) sb.AppendLine(BuildStatement(statement));

            result.BodyStopIndex = sb.Length - 1;

            result.Script = sb.ToString();

            return result;
        }

        public override ScriptBuildResult GenerateTriggerScripts(TriggerScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            var events = string.Join(" OR ", script.Events);

            sb.AppendLine($"CREATE OR REPLACE TRIGGER {script.NameWithSchema}");
            sb.AppendLine($"{script.Time} {events} ON {script.TableName}");
            sb.AppendLine("FOR EACH ROW");

            if (script.Condition != null) sb.AppendLine($"WHEN ({script.Condition})");

            foreach (var statement in script.Statements.Where(item => item is DeclareVariableStatement))
                sb.AppendLine(BuildStatement(statement));

            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements.Where(item => !(item is DeclareVariableStatement)))
                sb.AppendLine(BuildStatement(statement));

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END;");

            result.Script = sb.ToString();

            return result;
        }
    }
}