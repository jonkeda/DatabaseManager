using System;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model.Script;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class SqliteScriptBuildFactory : ScriptBuildFactory
    {
        public override DatabaseType DatabaseType => DatabaseType.Sqlite;

        public override ScriptBuildResult GenerateTriggerScripts(TriggerScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            var time = script.Time == TriggerTime.INSTEAD_OF ? "INSTEAD OF" : script.Time.ToString();
            var @event = script.Events.FirstOrDefault();
            var columnNames = @event == TriggerEvent.UPDATE ? $" {string.Join(",", script.ColumnNames)}" : "";
            var strEvent = @event == TriggerEvent.UPDATE ? "UPDATE OF" : @event.ToString();

            sb.AppendLine($"CREATE TRIGGER {script.Name}");
            sb.AppendLine($"{time} {strEvent}{columnNames} ON {script.TableName} FOR EACH ROW");

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

        public override ScriptBuildResult GenerateViewScripts(ViewScript script)
        {
            var result = new ScriptBuildResult();

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE VIEW {script.Name} AS");

            result.BodyStartIndex = sb.Length;

            foreach (var statement in script.Statements)
            {
                sb.AppendLine(BuildStatement(statement));
            }

            result.BodyStopIndex = sb.Length - 1;

            result.Script = sb.ToString();

            return result;
        }

        public override ScriptBuildResult GenerateRoutineScripts(RoutineScript script)
        {
            throw new NotSupportedException();
        }
    }
}