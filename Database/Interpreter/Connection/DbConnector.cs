using System.Data.Common;
using DatabaseInterpreter.Model;
using Databases.Handlers;

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
            _connectionString = connectionBuilder.BuildConnectionString(connectionInfo);
        }

        public DbConnection CreateConnection()
        {
            var lowerProviderName = _dbProvider.ProviderName.ToLower();
            var factory = SqlHandler.CreateConnection(lowerProviderName);

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