using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseInterpreter.Model;

namespace Databases.Config
{
    public class CreateTableOptionManager : ConfigManager
    {
        public const char OptionValueItemsSeperator = ';';

        private static readonly Dictionary<DatabaseType, CreateTableOption> DictCreateTableOption = new Dictionary<DatabaseType, CreateTableOption>();

        private static readonly object LockObj = new object();

        public static CreateTableOption GetCreateTableOption(DatabaseType databaseType)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (DictCreateTableOption.TryGetValue(databaseType, out var tableOption1))
            {
                return tableOption1;
            }
            lock (LockObj)
            {

                if (DictCreateTableOption.TryGetValue(databaseType, out var tableOption))
                {
                    return tableOption;
                }

                var filePath = Path.Combine(ConfigRootFolder, $"Option/CreateTableOption/{databaseType}.xml");

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var root = XDocument.Load(filePath).Root;

                var option = new CreateTableOption
                {
                    Items = root.Elements("item").Select(item => item.Value).ToList()
                };

                DictCreateTableOption.Add(databaseType, option);

                return option;
            }
        }
    }
}