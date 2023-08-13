using Databases.Handlers;
using Databases.Interpreter;

namespace Databases.ScriptGenerator
{
    public class DbScriptGeneratorHelper
    {
        public static DbScriptGenerator GetDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return SqlHandler.GetHandler(dbInterpreter.DatabaseType).CreateDbScriptGenerator(dbInterpreter);
        }
    }
}