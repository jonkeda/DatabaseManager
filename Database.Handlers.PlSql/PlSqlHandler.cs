using System.Data.Common;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using Databases.SqlAnalyser;
using Oracle.ManagedDataAccess.Client;
using SqlAnalyser.Core;

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
        { }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PlSqlAnalyser(content);
        }


        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new OracleDiagnosis(connectionInfo);
        }

        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new OracleInterpreter(connectionInfo, option);
        }

        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new OracleScriptGenerator(dbInterpreter);
        }

        protected override DbProviderFactory CreateDbProviderFactory(string providerName)
        {
            if (providerName.Contains("oracle"))
            {
                return new OracleClientFactory();
            }

            return null;
        }
    }
}