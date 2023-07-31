using System;
using System.Collections.Generic;
using System.Linq;
using Databases.Config;
using Databases.Interpreter;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.TableDesigner;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;

namespace Databases.Manager.Manager
{
    public class ColumnManager
    {
        public static List<TableColumnDesingerInfo> GetTableColumnDesingerInfos(DbInterpreter dbInterpreter,
            Table table, List<TableColumn> columns, List<TablePrimaryKey> primaryKeys)
        {
            var columnDesingerInfos = new List<TableColumnDesingerInfo>();
            var dataTypes = DataTypeManager.GetDataTypeSpecifications(dbInterpreter.DatabaseType)
                .Select(item => item.Name);

            foreach (var column in columns)
            {
                var dataTypeInfo = DataTypeHelper.GetDataTypeInfo(column.DataType);

                var columnDesingerInfo = new TableColumnDesingerInfo
                {
                    OldName = column.Name,
                    IsPrimary = primaryKeys.Any(item => item.Columns.Any(t => t.ColumnName == column.Name)),
                    Length = dbInterpreter.GetColumnDataLength(column)
                };

                ObjectHelper.CopyProperties(column, columnDesingerInfo);

                var dataType = DataTypeHelper.IsUserDefinedType(column)
                    ? column.DataType
                    : dataTypeInfo.DataType.ToLower();

                if (!dataTypes.Contains(dataType))
                {
                    dataTypeInfo = DataTypeHelper.GetDataTypeInfoByRegex(dataType);
                    dataType = dataTypeInfo.DataType;
                    columnDesingerInfo.Length = dataTypeInfo.Args;
                }

                columnDesingerInfo.DataType = dataType;

                columnDesingerInfo.ExtraPropertyInfo = new TableColumnExtraPropertyInfo();

                if (column.IsComputed)
                {
                    columnDesingerInfo.ExtraPropertyInfo.Expression = column.ComputeExp;
                }

                if (table.IdentitySeed.HasValue)
                {
                    columnDesingerInfo.ExtraPropertyInfo.Seed = table.IdentitySeed.Value;
                    columnDesingerInfo.ExtraPropertyInfo.Increment = table.IdentityIncrement.Value;
                }

                columnDesingerInfos.Add(columnDesingerInfo);
            }

            return columnDesingerInfos;
        }

        public static IEnumerable<DataTypeDesignerInfo> GetDataTypeInfos(DatabaseType databaseType)
        {
            var dataTypeDesignerInfos = new List<DataTypeDesignerInfo>();

            var dataTypeSpecifications = DataTypeManager.GetDataTypeSpecifications(databaseType);

            foreach (var dataTypeSpec in dataTypeSpecifications)
            {
                var dataTypeDesingerInfo = new DataTypeDesignerInfo();

                ObjectHelper.CopyProperties(dataTypeSpec, dataTypeDesingerInfo);

                dataTypeDesignerInfos.Add(dataTypeDesingerInfo);
            }

            return dataTypeDesignerInfos;
        }

        public static bool ValidateDataType(DatabaseType databaseType, TableColumnDesingerInfo columnDesingerInfo,
            out string message)
        {
            message = "";

            if (DataTypeHelper.IsUserDefinedType(columnDesingerInfo))
            {
                return true;
            }

            var columName = columnDesingerInfo.Name;
            var dataType = columnDesingerInfo.DataType;

            var dataTypeSpec = DataTypeManager.GetDataTypeSpecification(databaseType, dataType);

            if (dataTypeSpec == null)
            {
                message = $"Invalid data type:{dataType}";
                return false;
            }

            if (!string.IsNullOrEmpty(dataTypeSpec.Args))
            {
                var length = columnDesingerInfo?.Length?.Trim();

                if (string.IsNullOrEmpty(length) && dataTypeSpec.Optional)
                {
                    return true;
                }

                if (dataTypeSpec.AllowMax && !string.IsNullOrEmpty(length) && length.ToLower() == "max")
                {
                    return true;
                }

                var args = dataTypeSpec.Args;

                var argsNames = args.Split(',');
                var lengthItems = length?.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                if (argsNames.Length != lengthItems?.Length)
                {
                    if (argsNames.Length == 2 && lengthItems?.Length == 1)
                    {
                        lengthItems = new[] { lengthItems[0], "0" };
                    }
                    else
                    {
                        message = $"Length is invalid for column \"{columName}\", it's format should be:{args}";
                        return false;
                    }
                }

                var i = 0;

                foreach (var argName in argsNames)
                {
                    var lengthItem = lengthItems[i];

                    var range = DataTypeManager.GetArgumentRange(dataTypeSpec, argName);

                    if (range.HasValue)
                    {
                        int lenValue;

                        if (string.IsNullOrEmpty(lengthItem))
                        {
                            message = $"{argName} can't be empty!";
                            return false;
                        }

                        if (!int.TryParse(lengthItem, out lenValue))
                        {
                            message = $"\"{lengthItem}\" isn't a valid integer value for {argName}";
                            return false;
                        }

                        if (lenValue < range.Value.Min || lenValue > range.Value.Max)
                        {
                            message =
                                $"The \"{argName}\"'s range of column \"{columName}\" should be between {range.Value.Min} and {range.Value.Max}";
                            return false;
                        }
                    }

                    i++;
                }
            }

            return true;
        }

        public static void SetColumnLength(DatabaseType databaseType, TableColumn column, string length)
        {
            var dataType = column.DataType;
            var dataTypeSpec = DataTypeManager.GetDataTypeSpecification(databaseType, dataType);

            if (!string.IsNullOrEmpty(dataTypeSpec.MapTo))
            {
                column.DataType = dataTypeSpec.MapTo;
                return;
            }

            var args = dataTypeSpec.Args;

            if (string.IsNullOrEmpty(args))
            {
                return;
            }

            if (string.IsNullOrEmpty(length) && dataTypeSpec.Optional)
            {
                return;
            }

            var argsNames = args.Split(',');
            var lengthItems = length?.Split(',');

            var i = 0;

            foreach (var argName in argsNames)
            {
                if (lengthItems == null || i > lengthItems.Length - 1)
                {
                    continue;
                }

                var lengthItem = lengthItems[i];

                if (argName == "length")
                {
                    var isChar = DataTypeHelper.IsCharType(dataType);

                    if (isChar)
                    {
                        if (dataTypeSpec.AllowMax && lengthItem.ToLower() == "max")
                        {
                            column.MaxLength = -1;
                        }
                        else
                        {
                            column.MaxLength = long.Parse(lengthItem) * (DataTypeHelper.StartsWithN(dataType) ? 2 : 1);
                        }
                    }
                    else
                    {
                        if (lengthItem != "max")
                        {
                            column.MaxLength = long.Parse(lengthItem);
                        }
                        else
                        {
                            column.MaxLength = -1;
                        }
                    }
                }
                else if (argName == "precision" || argName == "dayScale")
                {
                    column.Precision = int.Parse(lengthItem);
                }
                else if (argName == "scale")
                {
                    column.Scale = int.Parse(lengthItem);
                }

                i++;
            }
        }
    }
}