using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.MySql
{
    public class MySqlHandler : SqlHandler<
        MySqlScriptBuildFactory, 
        MySqlStatementScriptBuilder,
        MySqlAnalyser,
        MySqlBackup>
    {
        public MySqlHandler() : base(DatabaseType.MySql)
        {

        }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new MySqlAnalyser(content);
        }        
    }
}
