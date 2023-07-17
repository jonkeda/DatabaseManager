using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class CreateTableOptionManager : ConfigManager
    {
        public const char OptionValueItemsSeperator = ';';

        private static Dictionary<DatabaseType, CreateTableOption> dictCreateTableOption;

        public static CreateTableOption GetCreateTableOption(DatabaseType databaseType)
        {
            if (dictCreateTableOption != null && dictCreateTableOption.TryGetValue(databaseType, out var tableOption))
                return tableOption;

            var filePath = Path.Combine(ConfigRootFolder, $"Option/CreateTableOption/{databaseType}.xml");

            if (!File.Exists(filePath)) return null;

            var root = XDocument.Load(filePath).Root;

            var option = new CreateTableOption
            {
                Items = root.Elements("item").Select(item => item.Value).ToList()
            };

            if (dictCreateTableOption == null)
                dictCreateTableOption = new Dictionary<DatabaseType, CreateTableOption>();

            dictCreateTableOption.Add(databaseType, option);

            return option;
        }
    }
}