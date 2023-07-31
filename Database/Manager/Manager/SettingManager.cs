using System.IO;
using Databases.Config;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.Setting;
using Newtonsoft.Json;

namespace Databases.Manager.Manager
{
    public class SettingManager : ConfigManager
    {
        static SettingManager()
        {
            LoadConfig();
        }

        public static Setting Setting { get; private set; } = new Setting();

        public static string ConfigFilePath => Path.Combine(ConfigRootFolder, "Setting.json");

        public static void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                Setting = (Setting)JsonConvert.DeserializeObject(File.ReadAllText(ConfigFilePath), typeof(Setting));
            }
        }

        public static void SaveConfig(Setting setting)
        {
            Setting = setting;
            var content = JsonConvert.SerializeObject(setting, Formatting.Indented);

            File.WriteAllText(ConfigFilePath, content);
        }

        public static DbInterpreterSetting GetInterpreterSetting()
        {
            var dbInterpreterSetting = new DbInterpreterSetting();

            ObjectHelper.CopyProperties(Setting, dbInterpreterSetting);

            return dbInterpreterSetting;
        }
    }
}