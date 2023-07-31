using System.Text;
using Databases.Connection;
using Databases.Model.Connection;

namespace Databases.Handlers.Sqlite
{
    public class SqliteConnectionStringBuilder : IConnectionBuilder
    {
        public string BuildConnectionString(ConnectionInfo connectionInfo)
        {
            var sb = new StringBuilder(
                $"Data Source={connectionInfo.Database};Password={connectionInfo.Password};Mode=ReadWriteCreate;");

            return sb.ToString();
        }
    }
}