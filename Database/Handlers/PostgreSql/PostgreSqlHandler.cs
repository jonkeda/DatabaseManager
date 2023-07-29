using SqlAnalyser.Core;
using DatabaseInterpreter.Model;

namespace Databases.Handlers.TSql
{
    public class PostgreSqlHandler : SqlHandler<
        PostgreSqlScriptBuildFactory, 
        PostgreSqlStatementScriptBuilder,
        PostgreSqlAnalyser>
    {
        public PostgreSqlHandler() : base(DatabaseType.Postgres)
        {

        }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PostgreSqlAnalyser(content);
        }

    }
}
