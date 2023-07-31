using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace Databases.Config
{
    public class FunctionManager : ConfigManager
    {
        private static readonly Dictionary<DatabaseType, List<FunctionSpecification>> FunctionSpecifications = new Dictionary<DatabaseType, List<FunctionSpecification>>();

        private static readonly object LockObj = new object();

        public static List<FunctionSpecification> GetFunctionSpecifications(DatabaseType dbType)
        {
            if (FunctionSpecifications.TryGetValue(dbType, out var specifications1))
            {
                return specifications1;
            }
            lock (LockObj)
            {
                if (FunctionSpecifications.TryGetValue(dbType, out var specifications))
                {
                    return specifications;
                }

                var filePath = Path.Combine(ConfigRootFolder, $"FunctionSpecification/{dbType}.xml");

                var doc = XDocument.Load(filePath);

                var functionSpecs = doc.Root.Elements("item").Select(item => new FunctionSpecification
                {
                    Name = item.Attribute("name").Value,
                    Args = item.Attribute("args").Value,
                    Delimiter = item.Attribute("delimiter")?.Value,
                    NoParenthesess = ValueHelper.IsTrueValue(item.Attribute("noParenthesess")?.Value),
                    IsString = ValueHelper.IsTrueValue(item.Attribute("isString")?.Value)
                }).ToList();

                FunctionSpecifications.Add(dbType, functionSpecs);

                return functionSpecs;
            }
        }
    }
}