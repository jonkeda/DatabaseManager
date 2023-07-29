using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.Sqlite
{
    public class SqliteHandler : SqlHandler<
        SqliteScriptBuildFactory, 
        SqliteStatementScriptBuilder,
        SqliteAnalyser,
        SqliteBackup>
    {
        public SqliteHandler() : base(DatabaseType.Sqlite)
        {

        }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new SqliteAnalyser(content);
        }

    }
}
