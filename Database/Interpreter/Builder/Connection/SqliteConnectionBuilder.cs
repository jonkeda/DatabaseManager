using System.Text;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
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