using System.Collections.Generic;
using System.IO;
using System.Linq;
using Databases.Model.Enum;

namespace Databases.Config
{
    public class KeywordManager : ConfigManager
    {
        public static readonly string KeywordFolder = Path.Combine(ConfigRootFolder, "Keyword");

        private static readonly object LockObj = new object();

        private static readonly Dictionary<DatabaseType, List<string>> Keywords = new Dictionary<DatabaseType, List<string>>();


        public static IEnumerable<string> GetKeywords(DatabaseType databaseType)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (Keywords.TryGetValue(databaseType, out var keywords1))
            {
                return keywords1;
            }
            lock (LockObj)
            {
                if (Keywords.TryGetValue(databaseType, out var keywords))
                {
                    return keywords;
                }
                var filePath = Path.Combine(KeywordFolder, $"{databaseType}.txt");

                List<string> items;
                if (File.Exists(filePath))
                {
                    items = File.ReadAllLines(filePath).Where(item => item.Length > 0).ToList();
                }
                else
                {
                    items = new List<string>();
                }
                Keywords.Add(databaseType, items);
                return items;
            }
        }
    }
}