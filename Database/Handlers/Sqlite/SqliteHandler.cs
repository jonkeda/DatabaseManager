using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.Sqlite
{
    public class SqliteHandler : SqlHandler<
        SqliteScriptBuildFactory, 
        SqliteStatementScriptBuilder,
        SqliteAnalyser,
        SqliteBackup,
        SqliteDiagnosis>
    {
        public SqliteHandler() : base(DatabaseType.Sqlite)
        {

        }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new SqliteAnalyser(content);
        }

        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new SqliteDiagnosis(connectionInfo);
        }

    }
}
