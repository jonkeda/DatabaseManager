using System.Data.Common;
using DatabaseInterpreter.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace DatabaseInterpreter.Core
{
    public class DbConnector
    {
        private readonly string _connectionString;
        private readonly IDbProvider _dbProvider;

        public DbConnector(IDbProvider dbProvider, string connectionString)
        {
            _dbProvider = dbProvider;
            _connectionString = connectionString;
        }

        public DbConnector(IDbProvider dbProvider, IConnectionBuilder connectionBuilder, ConnectionInfo connectionInfo)
        {
            _dbProvider = dbProvider;
            _connectionString = connectionBuilder.BuildConntionString(connectionInfo);
        }

        public DbConnection CreateConnection()
        {
            DbProviderFactory factory = null;

            var lowerProviderName = _dbProvider.ProviderName.ToLower();
            if (lowerProviderName.Contains("oracle"))
                factory = new OracleClientFactory();
            else if (lowerProviderName.Contains("mysql"))
                factory = MySqlConnectorFactory.Instance;
            else if (lowerProviderName.Contains("sqlclient"))
                factory = SqlClientFactory.Instance;
            else if (lowerProviderName.Contains("npgsql"))
                factory = NpgsqlFactory.Instance;
            else if (lowerProviderName.Contains("sqlite")) factory = SqliteFactory.Instance;

            var connection = factory.CreateConnection();

            if (connection != null)
            {
                connection.ConnectionString = _connectionString;
                return connection;
            }

            return null;
        }
    }
}