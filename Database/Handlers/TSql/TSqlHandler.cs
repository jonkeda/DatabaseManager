using SqlAnalyser.Core;
using DatabaseInterpreter.Model;

namespace Databases.Handlers.TSql
{
    public class TSqlHandler : SqlHandler<
        TSqlScriptBuildFactory, 
        TSqlStatementScriptBuilder, 
        TSqlAnalyser>
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
