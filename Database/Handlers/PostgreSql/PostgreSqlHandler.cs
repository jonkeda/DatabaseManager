using DatabaseInterpreter.Core;
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


        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new PostgresInterpreter(connectionInfo, option);
        }


        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new PostgresScriptGenerator(dbInterpreter);
        }

    }
}
