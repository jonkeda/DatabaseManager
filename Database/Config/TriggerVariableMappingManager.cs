using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseConverter.Model;

namespace Databases.Config
{
    public class TriggerVariableMappingManager : ConfigManager
    {
        private static List<IEnumerable<VariableMapping>> _variableMappings;

        public static string TriggerVariableMappingFilePath =>
            Path.Combine(ConfigRootFolder, "TriggerVariableMapping.xml");

        private static readonly object LockObj = new object();

        public static List<IEnumerable<VariableMapping>> VariableMappings
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_variableMappings == null)
                {
                    lock (LockObj)
                    {
                        if (_variableMappings == null)
                        {
                            _variableMappings = GetVariableMappings();
                        }
                    }
                }

                return _variableMappings;
            }
        }

        public static List<IEnumerable<VariableMapping>> GetVariableMappings()
        {
            var doc = XDocument.Load(TriggerVariableMappingFilePath);
            return doc.Root.Elements("mapping").Select(item =>
                    item.Elements().Select(t => new VariableMapping { DbType = t.Name.ToString(), Variable = t.Value }))
                .ToList();
        }
    }
}