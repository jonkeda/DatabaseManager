using Databases.Model.Account;

namespace Databases.Model.Connection
{
    public class ConnectionInfo : DatabaseAccountInfo
    {
        public string Database { get; set; }
    }
}