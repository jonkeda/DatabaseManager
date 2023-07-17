using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Core.Model;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class PostgreSqlScriptBuildFactory : ScriptBuildFactory
    {
        public override DatabaseType DatabaseType => DatabaseType.Postgres;

        protected override void PreHandleStatements(List<Statement> statements)
        {
            base.PreHandleStatements(statements);

            PreHandle();
        }

        protected override void PostHandleStatements(StringBuilder sb)
        {
            base.PostHandleStatements(sb);

            PostHandle(sb);
        }

        private void PreHandle()
        {
            StatementBuilder.Option.CollectDeclareStatement = true;
            StatementBuilder.Option.NotBuildDeclareStatement = true;
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

                    sbDeclare.AppendLine(BuildStatement(declareStatement, routineType).Trim());
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

            PreHandle();

            var sb = new StringBuilder();

            sb.Append($"CREATE OR REPLACE {script.Type.ToString()} {script.NameWithSchema}");

            if (script.Parameters.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("(");

                var i = 0;
                foreach (var parameter in script.Parameters)
                {
                    var parameterType = parameter.ParameterType;

                    var dataType = parameter.DataType.Symbol;
                    var defaultValue = parameter.DefaultValue == null ? "" : $" DEFAULT {parameter.DefaultValue}";
                    var strParameterType = "";

                    var parenthesesIndex = dataType.IndexOf("(");

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
            else
            {
                sb.Append("()" + Environment.NewLine);
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

                    sb.AppendLine($"RETURNS {dataType}");
                }
                else if (script.ReturnTable != null)
                {
                    if (script.ReturnTable.Columns.Count > 0)
                        sb.AppendLine(
                            $"RETURNS TABLE({string.Join(",", script.ReturnTable.Columns.Select(item => GetColumnInfo(item)))})");
                }
                else
                {
                    if (script.Statements.Count > 0 && script.Statements.First() is SelectStatement select)
                        sb.AppendLine(
                            $"RETURNS TABLE({string.Join(",", select.Columns.Select(item => $"{item.FieldName} character varying"))})");
                }
            }

            sb.AppendLine("LANGUAGE 'plpgsql'");
            sb.AppendLine("AS");
            sb.AppendLine("$$");

            var declareStartIndex = sb.Length;

            sb.AppendLine("BEGIN");

            result.BodyStartIndex = sb.Length;

            if (script.Type == RoutineType.FUNCTION)
                if (script.ReturnDataType == null)
                    sb.Append("RETURN QUERY ");

            FetchCursorStatement fetchCursorStatement = null;

            foreach (var statement in script.Statements)
            {
                if (statement is FetchCursorStatement fetch)
                {
                    fetchCursorStatement = fetch;
                    continue;
                }

                if (statement is WhileStatement @while)
                {
                    var fs =
                        @while.Statements.FirstOrDefault(item => item is FetchCursorStatement) as FetchCursorStatement;

                    if (fetchCursorStatement != null && fs != null)
                    {
                        @while.Condition.Symbol = "1=1";

                        if (fs.Variables.Count == 0)
                        {
                            @while.Statements.Insert(0,
                                new LoopExitStatement { Condition = new TokenInfo("EXIT WHEN NOT FOUND;") });
                            @while.Statements.Insert(0, fetchCursorStatement);
                        }
                    }
                }

                sb.AppendLine(BuildStatement(statement, script.Type));
            }

            result.BodyStopIndex = sb.Length - 1;

            sb.AppendLine("END");
            sb.AppendLine("$$;");

            PostHandle(sb, script.Type, result, declareStartIndex);

            result.Script = sb.ToString();

            return result;
        }

        private string GetColumnInfo(ColumnInfo columnInfo)
        {
            var name = columnInfo.Name.FieldName;
            var dataType = string.IsNullOrEmpty(columnInfo.DataType?.Symbol)
                ? "character varying"
                : columnInfo.DataType.Symbol;

            return $"{name} {dataType}";
        }

        public override ScriptBuildResult GenearteViewScripts(ViewScript script)
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

        public override ScriptBuildResult GenearteTriggerScripts(TriggerScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            var events = string.Join(" OR ", script.Events);

            sb.AppendLine($"CREATE OR REPLACE TRIGGER {script.Name}");
            sb.AppendLine($"{script.Time} {events} ON {script.TableName.NameWithSchema}");
            sb.AppendLine("FOR EACH ROW");

            result.BodyStartIndex = sb.Length;

            sb.AppendLine($"EXECUTE PROCEDURE {script.FunctionName?.NameWithSchema}();");

            result.BodyStopIndex = sb.Length - 1;

            result.Script = sb.ToString();

            return result;
        }
    }
}