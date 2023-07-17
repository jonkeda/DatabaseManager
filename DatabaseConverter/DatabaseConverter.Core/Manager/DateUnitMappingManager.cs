using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DatabaseConverter.Core.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public class DateUnitMappingManager : ConfigManager
    {
        public static List<DateUnitMapping> _dateUnitMappings;
        public static string DateUnitMappingFilePath => Path.Combine(ConfigRootFolder, "DateUnitMapping.xml");

        public static List<DateUnitMapping> DateUnitMappings
        {
            get
            {
                if (_dateUnitMappings == null) _dateUnitMappings = GetDateUnitMappings();

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
                var mapping = new DateUnitMapping();

                mapping.Name = element.Attribute("name").Value;

                var items = element.Elements();

                foreach (var item in items)
                {
                    var mappingItem = new DateUnitMappingItem();

                    mappingItem.DbType = item.Name.ToString();
                    mappingItem.Unit = item.Value;
                    mappingItem.CaseSensitive = ValueHelper.IsTrueValue(item.Attribute("caseSensitive")?.Value);
                    mappingItem.Formal = ValueHelper.IsTrueValue(item.Attribute("formal")?.Value);

                    mapping.Items.Add(mappingItem);
                }

                mappings.Add(mapping);
            }

            return mappings;
        }
    }
}