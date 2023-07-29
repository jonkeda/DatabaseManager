﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public class DataTypeManager : ConfigManager
    {
        public const char ArugumentRangeItemDelimiter = ',';
        public const char ArugumentRangeValueDelimiter = '~';
        private static Dictionary<DatabaseType, List<DataTypeSpecification>> _dataTypeSpecifications;

        public static IEnumerable<DataTypeSpecification> GetDataTypeSpecifications(DatabaseType databaseType)
        {
            if (_dataTypeSpecifications != null && _dataTypeSpecifications.TryGetValue(databaseType, out var specifications))
                return specifications;

            var filePath = Path.Combine(ConfigRootFolder, $"DataTypeSpecification/{databaseType}.xml");

            if (!File.Exists(filePath)) return Enumerable.Empty<DataTypeSpecification>();

            var doc = XDocument.Load(filePath);

            var functionSpecs = doc.Root.Elements("item").Select(item => new DataTypeSpecification
            {
                Name = item.Attribute("name").Value,
                Format = item.Attribute("format")?.Value,
                Args = item.Attribute("args")?.Value,
                Range = item.Attribute("range")?.Value,
                Optional = IsTrueValue(item.Attribute("optional")),
                Default = item.Attribute("default")?.Value,
                DisplayDefault = item.Attribute("displayDefault")?.Value,
                AllowMax = IsTrueValue(item.Attribute("allowMax")),
                MapTo = item.Attribute("mapTo")?.Value,
                IndexForbidden = IsTrueValue(item.Attribute("indexForbidden")),
                AllowIdentity = IsTrueValue(item.Attribute("allowIdentity"))
            }).ToList();

            functionSpecs.ForEach(item => ParseArgument(item));

            if (_dataTypeSpecifications == null)
                _dataTypeSpecifications = new Dictionary<DatabaseType, List<DataTypeSpecification>>();

            _dataTypeSpecifications.Add(databaseType, functionSpecs);

            return functionSpecs;
        }

        public static DataTypeSpecification GetDataTypeSpecification(DatabaseType databaseType, string dataType)
        {
            return GetDataTypeSpecifications(databaseType)
                .FirstOrDefault(item => item.Name.ToLower() == dataType.ToLower().Trim());
        }

        private static bool IsTrueValue(XAttribute attribute)
        {
            return ValueHelper.IsTrueValue(attribute?.Value);
        }

        public static DataTypeSpecification ParseArgument(DataTypeSpecification dataTypeSpecification)
        {
            if (string.IsNullOrEmpty(dataTypeSpecification.Args) || dataTypeSpecification.Arguments.Count > 0)
                return dataTypeSpecification;

            if (!string.IsNullOrEmpty(dataTypeSpecification.Range))
            {
                var argItems = dataTypeSpecification.Args.Split(ArugumentRangeItemDelimiter);
                var rangeItems = dataTypeSpecification.Range.Split(ArugumentRangeItemDelimiter);

                var i = 0;
                foreach (var argItem in argItems)
                {
                    var argument = new DataTypeArgument { Name = argItem };

                    if (i < rangeItems.Length)
                    {
                        var range = new ArgumentRange();

                        var rangeValues = rangeItems[i].Split(ArugumentRangeValueDelimiter);

                        range.Min = int.Parse(rangeValues[0]);

                        if (rangeValues.Length > 1)
                            range.Max = int.Parse(rangeValues[1]);
                        else
                            range.Max = range.Min;

                        argument.Range = range;
                    }

                    dataTypeSpecification.Arguments.Add(argument);

                    i++;
                }
            }

            return dataTypeSpecification;
        }

        public static ArgumentRange? GetArgumentRange(DataTypeSpecification dataTypeSpecification, string argumentName)
        {
            var range = default(ArgumentRange?);

            if (dataTypeSpecification.Arguments.Any(item => item.Name.ToLower() == argumentName.ToLower()))
                return dataTypeSpecification.Arguments
                    .FirstOrDefault(item => item.Name.ToLower() == argumentName.ToLower()).Range;

            return range;
        }
    }
}