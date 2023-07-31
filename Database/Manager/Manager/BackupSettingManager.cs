using System.Collections.Generic;
using System.IO;
using Databases.Config;
using Databases.Manager.Model.Setting;
using Newtonsoft.Json;

namespace Databases.Manager.Manager
{
    public class BackupSettingManager : ConfigManager
    {
        public static string ConfigFilePath => Path.Combine(ConfigRootFolder, "BackupSetting.json");

        public static List<BackupSetting> GetSettings()
        {
            if (File.Exists(ConfigFilePath))
            {
                return (List<BackupSetting>)JsonConvert.DeserializeObject(File.ReadAllText(ConfigFilePath),
                    typeof(List<BackupSetting>));
            }

            return new List<BackupSetting>();
        }

        public static void SaveConfig(List<BackupSetting> settings)
        {
            var content = JsonConvert.SerializeObject(settings, Formatting.Indented);

            File.WriteAllText(ConfigFilePath, content);
        }
    }
}