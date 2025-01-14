﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Databases.Converter.Model.Mappings;
using Databases.Model.Enum;

namespace Databases.Config
{
    public class DataTypeMappingManager : ConfigManager
    {
        private static Dictionary<(DatabaseType SourceDbType, DatabaseType TargetDbType), List<DataTypeMapping>>
            _dataTypeMappings;

        private static readonly object LockObj = new object();


        public static List<DataTypeMapping> GetDataTypeMappings(DatabaseType sourceDatabaseType,
            DatabaseType targetDatabaseType)
        {
            (DatabaseType sourceDbType, DatabaseType targetDbType) dbTypeMap = (sourceDatabaseType, targetDatabaseType);

            if (_dataTypeMappings != null && _dataTypeMappings.TryGetValue(dbTypeMap, out var typeMappings1))
            {
                return typeMappings1;
            }

            lock (LockObj)
            {
                if (_dataTypeMappings != null && _dataTypeMappings.TryGetValue(dbTypeMap, out var typeMappings))
                {
                    return typeMappings;
                }

                var dataTypeMappingFilePath = Path.Combine(ConfigRootFolder,
                    $"DataTypeMapping/{sourceDatabaseType}2{targetDatabaseType}.xml");

                if (!File.Exists(dataTypeMappingFilePath))
                {
                    throw new Exception($"No such file:{dataTypeMappingFilePath}");
                }

                var dataTypeMappingDoc = XDocument.Load(dataTypeMappingFilePath);

                var mappings = dataTypeMappingDoc.Root.Elements("mapping").Select(item =>
                        new DataTypeMapping
                        {
                            Source = new DataTypeMappingSource(item),
                            Target = ParseTarget(item),
                            Specials = item.Elements("special")?.Select(t => new DataTypeMappingSpecial(t)).ToList()
                        })
                    .ToList();

                if (_dataTypeMappings == null)
                {
                    _dataTypeMappings =
                        new Dictionary<(DatabaseType SourceDbType, DatabaseType TargetDbType), List<DataTypeMapping>>();
                }

                _dataTypeMappings.Add(dbTypeMap, mappings);


                return mappings;
            }
        }

        private static DataTypeMappingTarget ParseTarget(XElement element)
        {
            var target = new DataTypeMappingTarget(element);

            if (!string.IsNullOrEmpty(target.Args))
            {
                var items = target.Args.Split(',');

                foreach (var item in items)
                {
                    var nvs = item.Split(':');

                    var arg = new DataTypeMappingArgument { Name = nvs[0], Value = nvs[1] };

                    target.Arguments.Add(arg);
                }
            }

            return target;
        }
    }
}