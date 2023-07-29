using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.TSql
{
    public class PostgreSqlHandler : SqlHandler<
        PostgreSqlScriptBuildFactory, 
        PostgreSqlStatementScriptBuilder,
        PostgreSqlAnalyser,
        PostgresBackup, 
        PostgresDiagnosis>
    {
        public PostgreSqlHandler() : base(DatabaseType.Postgres)
        {

        }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PostgreSqlAnalyser(content);
        }


        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new PostgresDiagnosis(connectionInfo);
        }

    }
}
