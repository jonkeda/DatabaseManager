using Databases.Interpreter;

namespace Databases.Handlers.Sqlite
{
    public class SqliteProvider : IDbProvider
    {
        public string ProviderName => "Microsoft.Data.Sqlite";
    }
}