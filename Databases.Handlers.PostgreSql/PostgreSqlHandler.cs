using System.Data.Common;
using Databases.Interpreter;
using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.ScriptGenerator;
using Databases.SqlAnalyser;
using Npgsql;

namespace Databases.Handlers.PostgreSql
{
    public class PostgreSqlHandler : SqlHandler<
        PostgreSqlScriptBuildFactory,
        PostgreSqlStatementScriptBuilder,
        PostgreSqlAnalyser,
        PostgresBackup,
        PostgresDiagnosis>
    {
        public PostgreSqlHandler() : base(DatabaseType.Postgres)
        { }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new PostgreSqlAnalyser(content);
        }


        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new PostgresDiagnosis(connectionInfo);
        }


        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new PostgresInterpreter(connectionInfo, option);
        }


        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new PostgresScriptGenerator(dbInterpreter);
        }

        protected override DbProviderFactory CreateDbProviderFactory(string providerName)
        {
            if (providerName.Contains("npgsql"))
            {
                return NpgsqlFactory.Instance;
            }

            return null;
        }
    }
}