using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.PlSql
{
    public class PlSqlHandler : SqlHandler<
        PlSqlScriptBuildFactory, 
        PlSqlStatementScriptBuilder, 
        PlSqlAnalyser,
        OracleBackup, 
        OracleDiagnosis>
    {
        public PlSqlHandler() : base(DatabaseType.Oracle)
        {

        }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PlSqlAnalyser(content);
        }


        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new OracleDiagnosis(connectionInfo);
        }

    }
}
