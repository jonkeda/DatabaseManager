using Databases.Interpreter;

namespace Databases.Handlers.PlSql
{
    public class OracleProvider : IDbProvider
    {
        public string ProviderName => "Oracle.ManagedDataAccess.Client";
    }
}