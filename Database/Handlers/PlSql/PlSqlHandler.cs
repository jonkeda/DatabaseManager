using SqlAnalyser.Core;
using DatabaseInterpreter.Model;

namespace Databases.Handlers.PlSql
{
    public class PlSqlHandler : SqlHandler<
        PlSqlScriptBuildFactory, 
        PlSqlStatementScriptBuilder, 
        PlSqlAnalyser>
    {
        public PlSqlHandler() : base(DatabaseType.Oracle)
        {

        }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PlSqlAnalyser(content);
        }

    }
}
