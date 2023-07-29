using SqlAnalyser.Core;
using DatabaseInterpreter.Model;

namespace Databases.Handlers.Sqlite
{
    public class SqliteHandler : SqlHandler<
        SqliteScriptBuildFactory, 
        SqliteStatementScriptBuilder,
        SqliteAnalyser>
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
