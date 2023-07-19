using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public interface IConnectionBuilder
    {
        string BuildConnectionString(ConnectionInfo connectionInfo);
    }
}