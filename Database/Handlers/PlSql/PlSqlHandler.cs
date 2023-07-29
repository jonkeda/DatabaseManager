using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.PlSql
{
    public class PlSqlHandler : SqlHandler<
        PlSqlScriptBuildFactory, 
        PlSqlStatementScriptBuilder, 
        PlSqlAnalyser,
        OracleBackup>
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
