using Databases.Handlers;

namespace DatabaseInterpreter.Core
{
    public class DbScriptGeneratorHelper
    {
        public static DbScriptGenerator GetDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return SqlHandler.GetHandler(dbInterpreter.DatabaseType).CreateDbScriptGenerator(dbInterpreter);
        }
    }
}