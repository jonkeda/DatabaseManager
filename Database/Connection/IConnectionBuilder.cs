using Databases.Model.Connection;

namespace Databases.Connection
{
    public interface IConnectionBuilder
    {
        string BuildConnectionString(ConnectionInfo connectionInfo);
    }
}