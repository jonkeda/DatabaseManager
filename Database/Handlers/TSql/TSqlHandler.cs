using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.TSql
{
    public class TSqlHandler : SqlHandler<
        TSqlScriptBuildFactory, 
        TSqlStatementScriptBuilder, 
        TSqlAnalyser,
        SqlServerBackup>
    {
        public TSqlHandler() : base(DatabaseType.SqlServer)
        {

        }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new TSqlAnalyser(content);
        }

    }
}
