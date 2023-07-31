using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Databases.Converter.Model.Mappings;
using Databases.Interpreter.Utility.Helper;

namespace Databases.Config
{
    public class DateUnitMappingManager : ConfigManager
    {
        public static List<DateUnitMapping> _dateUnitMappings;
        public static string DateUnitMappingFilePath => Path.Combine(ConfigRootFolder, "DateUnitMapping.xml");

        private static readonly object LockObj = new object();

        public static List<DateUnitMapping> DateUnitMappings
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_dateUnitMappings == null)
                {
                    lock (LockObj)
                    {
                        if (_dateUnitMappings == null)
                        {
                            _dateUnitMappings = GetDateUnitMappings();
                        }
                    }
                }

                return _dateUnitMappings;
            }
        }

        public static List<DateUnitMapping> GetDateUnitMappings()
        {
            var mappings = new List<DateUnitMapping>();

            var doc = XDocument.Load(DateUnitMappingFilePath);

            var elements = doc.Root.Elements("mapping");

            foreach (var element in elements)
            {
                var mapping = new DateUnitMapping
                {
                    Name = element.Attribute("name").Value
                };

                var items = element.Elements();

                foreach (var item in items)
                {
                    var mappingItem = new DateUnitMappingItem
                    {
                        DbType = item.Name.ToString(),
                        Unit = item.Value,
                        CaseSensitive = ValueHelper.IsTrueValue(item.Attribute("caseSensitive")?.Value),
                        Formal = ValueHelper.IsTrueValue(item.Attribute("formal")?.Value)
                    };

                    mapping.Items.Add(mappingItem);
                }

                mappings.Add(mapping);
            }

            return mappings;
        }
    }
}