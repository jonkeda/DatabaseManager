using System.IO;
using DatabaseInterpreter.Utility;

namespace Databases.Config
{
    public class ConfigManager
    {
        public static string ConfigRootFolder => Path.Combine(PathHelper.GetAssemblyFolder(), "Config");
    }
}