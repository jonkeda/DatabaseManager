﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class FunctionManager : ConfigManager
    {
        private static Dictionary<DatabaseType, List<FunctionSpecification>> _functionSpecifications;

        public static List<FunctionSpecification> GetFunctionSpecifications(DatabaseType dbType)
        {
            if (_functionSpecifications != null && _functionSpecifications.TryGetValue(dbType, out var specifications))
                return specifications;

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

            if (_functionSpecifications == null)
                _functionSpecifications = new Dictionary<DatabaseType, List<FunctionSpecification>>();

            _functionSpecifications.Add(dbType, functionSpecs);

            return functionSpecs;
        }
    }
}