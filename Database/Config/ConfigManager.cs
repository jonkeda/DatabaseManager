using System.IO;
using Databases.Interpreter.Utility.Helper;

namespace Databases.Config
{
    public class ConfigManager
    {
        public static string ConfigRootFolder => Path.Combine(PathHelper.GetAssemblyFolder(), "Config");
    }
}