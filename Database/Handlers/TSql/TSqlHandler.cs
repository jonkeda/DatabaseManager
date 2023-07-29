using DatabaseInterpreter.Core;
using SqlAnalyser.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Databases.Handlers.TSql
{
    public class TSqlHandler : SqlHandler<
        TSqlScriptBuildFactory, 
        TSqlStatementScriptBuilder, 
        TSqlAnalyser,
        SqlServerBackup, 
        SqlServerDiagnosis>
    {
        public TSqlHandler() : base(DatabaseType.SqlServer)
        {

        }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new TSqlAnalyser(content);
        }

        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new SqlServerDiagnosis(connectionInfo);
        }


        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new SqlServerInterpreter(connectionInfo, option);
        }

        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new SqlServerScriptGenerator(dbInterpreter);
        }

    }
}
