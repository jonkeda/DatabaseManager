using Databases.Interpreter;

namespace Databases.Handlers.TSql
{
    public class SqlServerProvider : IDbProvider
    {
        public string ProviderName => "Microsoft.Data.SqlClient";
    }
}