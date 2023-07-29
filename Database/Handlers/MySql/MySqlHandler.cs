using SqlAnalyser.Core;
using DatabaseInterpreter.Model;

namespace Databases.Handlers.MySql
{
    public class MySqlHandler : SqlHandler<
        MySqlScriptBuildFactory, 
        MySqlStatementScriptBuilder,
        MySqlAnalyser>
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
