using System.IO;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class ConfigManager
    {
        public static string ConfigRootFolder => Path.Combine(PathHelper.GetAssemblyFolder(), "Config");
    }
}