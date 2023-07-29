using DatabaseInterpreter.Model;

namespace Databases.Interpreter.Builder
{
    public interface IConnectionBuilder
    {
        string BuildConnectionString(ConnectionInfo connectionInfo);
    }
}