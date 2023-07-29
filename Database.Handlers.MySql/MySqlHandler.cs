using System.Data.Common;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using Databases.SqlAnalyser;
using MySqlConnector;
using SqlAnalyser.Core;

namespace Databases.Handlers.MySql
{
    public class MySqlHandler : SqlHandler<
        MySqlScriptBuildFactory,
        MySqlStatementScriptBuilder,
        MySqlAnalyser,
        MySqlBackup,
        MySqlDiagnosis>
    {
        public MySqlHandler() : base(DatabaseType.MySql)
        {
        }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new MySqlAnalyser(content);
        }

        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new MySqlDiagnosis(connectionInfo);
        }

        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new MySqlInterpreter(connectionInfo, option);
        }

        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new MySqlScriptGenerator(dbInterpreter);
        }

        protected override DbProviderFactory CreateDbProviderFactory(string providerName)
        {
            if (providerName.Contains("mysql"))
                return MySqlConnectorFactory.Instance;
            return null;
        }
    }
}