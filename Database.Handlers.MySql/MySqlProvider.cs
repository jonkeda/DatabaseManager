using Databases.Interpreter;

namespace Databases.Handlers.MySql
{
    public class MySqlProvider : IDbProvider
    {
        public string ProviderName => "MySql.Data.MySqlClient";
    }
}