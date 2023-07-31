using Databases.Interpreter;

namespace Databases.Handlers.PostgreSql
{
    public class PostgresProvider : IDbProvider
    {
        public string ProviderName => "Npgsql";
    }
}