using System;
using System.Collections.Generic;
using System.Text;
using DatabaseInterpreter.Model;
using Databases.Handlers;
using SqlAnalyser.Core.Model;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public abstract class ScriptBuildFactory
    {
        private StatementScriptBuilder statementBuilder;
        public abstract DatabaseType DatabaseType { get; }
        public StatementScriptBuilderOption ScriptBuilderOption { get; set; } = new StatementScriptBuilderOption();

        public StatementScriptBuilder StatementBuilder
        {
            get
            {
                if (statementBuilder == null) statementBuilder = GetStatementBuilder();

                return statementBuilder;
            }
        }

        public abstract ScriptBuildResult GenerateRoutineScripts(RoutineScript script);
        public abstract ScriptBuildResult GenerateViewScripts(ViewScript script);
        public abstract ScriptBuildResult GenerateTriggerScripts(TriggerScript script);

        private StatementScriptBuilder GetStatementBuilder()
        {
            StatementScriptBuilder builder = SqlHandler.GetHandler(DatabaseType).CreateStatementScriptBuilder();

            builder.Option = ScriptBuilderOption;

            return builder;
        }

        public string BuildStatement(Statement statement, RoutineType routineType = RoutineType.UNKNOWN)
        {
            StatementBuilder.RoutineType = routineType;

            StatementBuilder.Clear();

            StatementBuilder.Build(statement);

            return StatementBuilder.ToString();
        }

        public virtual ScriptBuildResult GenerateScripts(CommonScript script)
        {
            ScriptBuildResult result;

            if (script is RoutineScript routineScript)
                result = GenerateRoutineScripts(routineScript);
            else if (script is ViewScript viewScript)
                result = GenerateViewScripts(viewScript);
            else if (script is TriggerScript triggerScript)
                result = GenerateTriggerScripts(triggerScript);
            else if (script is CommonScript commonScript)
                result = GenerateCommonScripts(commonScript);
            else
                throw new NotSupportedException($"Not support generate scripts for type: {script.GetType()}.");

            if (statementBuilder != null && statementBuilder.Replacements.Count > 0)
                foreach (var kp in statementBuilder.Replacements)
                    result.Script = AnalyserHelper.ReplaceSymbol(result.Script, kp.Key, kp.Value);

            statementBuilder?.Dispose();

            return result;
        }

        protected virtual void PreHandleStatements(List<Statement> statements)
        {
        }

        protected virtual void PostHandleStatements(StringBuilder sb)
        {
        }

        protected virtual ScriptBuildResult GenerateCommonScripts(CommonScript script)
        {
            PreHandleStatements(script.Statements);

            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            foreach (var statement in script.Statements) sb.AppendLine(BuildStatement(statement));

            PostHandleStatements(sb);

            result.Script = sb.ToString().Trim();

            statementBuilder?.Dispose();

            return result;
        }
    }
}