using System.Collections.Generic;
using System.IO;
using DatabaseManager.Model;
using Databases.Config;
using Newtonsoft.Json;

namespace DatabaseManager.Core
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