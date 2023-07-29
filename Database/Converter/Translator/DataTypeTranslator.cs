using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using NCalc;

namespace DatabaseConverter.Core
{
    public class DataTypeTranslator : DbObjectTokenTranslator
    {
        private readonly IEnumerable<DataTypeSpecification> sourceDataTypeSpecs;
        private readonly IEnumerable<DataTypeSpecification> targetDataTypeSpecs;

        public DataTypeTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter) : base(
            sourceInterpreter, targetInterpreter)
        {
            sourceDataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(sourceDbType);
            targetDataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(targetDbType);

            LoadMappings();
        }

        public void Translate(DataTypeInfo dataTypeInfo)
        {
            var originalDataType = dataTypeInfo.DataType;

            var dti = sourceDbInterpreter.GetDataTypeInfo(originalDataType);
            var sourceDataType = dti.DataType;

            dataTypeInfo.DataType = sourceDataType;

            var sourceDataTypeSpec = GetDataTypeSpecification(sourceDataTypeSpecs, sourceDataType);

            if (!string.IsNullOrEmpty(dti.Args))
            {
                if (sourceDataTypeSpec.Args == "scale")
                    dataTypeInfo.Scale = int.Parse(dti.Args);
                else if (sourceDataTypeSpec.Args == "length")
                    if (dataTypeInfo.MaxLength == null)
                        dataTypeInfo.MaxLength = int.Parse(dti.Args);
            }

            var dataTypeMapping = dataTypeMappings.FirstOrDefault(item =>
                item.Source.Type?.ToLower() == dataTypeInfo.DataType?.ToLower()
                || (item.Source.IsExpression &&
                    Regex.IsMatch(dataTypeInfo.DataType, item.Source.Type, RegexOptions.IgnoreCase))
            );

            if (dataTypeMapping != null)
            {
                var sourceMapping = dataTypeMapping.Source;
                var targetMapping = dataTypeMapping.Target;
                var targetDataType = targetMapping.Type;

                var targetDataTypeSpec = GetDataTypeSpecification(targetDataTypeSpecs, targetDataType);

                if (targetDataTypeSpec == null)
                    throw new Exception($"No type '{targetDataType}' defined for '{targetDbType}'.");

                dataTypeInfo.DataType = targetDataType;

                var isChar = DataTypeHelper.IsCharType(dataTypeInfo.DataType);
                var isBinary = DataTypeHelper.IsBinaryType(dataTypeInfo.DataType);

                if (isChar || isBinary)
                {
                    var noLength = false;

                    if (isChar)
                    {
                        if (!string.IsNullOrEmpty(targetMapping.Length))
                        {
                            dataTypeInfo.MaxLength = int.Parse(targetMapping.Length);

                            if (!DataTypeHelper.StartsWithN(sourceDataType) &&
                                DataTypeHelper.StartsWithN(targetDataType)) dataTypeInfo.MaxLength *= 2;
                        }
                        else
                        {
                            if (DataTypeHelper.StartsWithN(sourceDataType) &&
                                !DataTypeHelper.StartsWithN(targetDataType))
                                if (!Option?.NcharToDoubleChar == true)
                                    if (dataTypeInfo.MaxLength > 0 && dataTypeInfo.MaxLength % 2 == 0)
                                        dataTypeInfo.MaxLength /= 2;
                        }
                    }

                    if (dataTypeMapping.Specials != null && dataTypeMapping.Specials.Count > 0)
                    {
                        var special =
                            dataTypeMapping.Specials.FirstOrDefault(item =>
                                IsSpecialMaxLengthMatched(item, dataTypeInfo));

                        if (special != null)
                        {
                            if (!string.IsNullOrEmpty(special.Type)) dataTypeInfo.DataType = special.Type;

                            if (!string.IsNullOrEmpty(special.TargetMaxLength))
                            {
                                dataTypeInfo.MaxLength = int.Parse(special.TargetMaxLength);
                            }
                            else
                            {
                                noLength = special.NoLength;
                                dataTypeInfo.MaxLength = -1;
                            }
                        }
                    }

                    if (!noLength)
                        if (dataTypeInfo.MaxLength == -1)
                        {
                            var sourceLengthRange = DataTypeManager.GetArgumentRange(sourceDataTypeSpec, "length");

                            if (sourceLengthRange.HasValue) dataTypeInfo.MaxLength = sourceLengthRange.Value.Max;
                        }

                    var targetLengthRange = DataTypeManager.GetArgumentRange(targetDataTypeSpec, "length");

                    if (targetLengthRange.HasValue)
                    {
                        var targetMaxLength = targetLengthRange.Value.Max;

                        if (DataTypeHelper.StartsWithN(targetDataTypeSpec.Name)) targetMaxLength *= 2;

                        if (dataTypeInfo.MaxLength > targetMaxLength)
                            if (!string.IsNullOrEmpty(targetMapping.Substitute))
                            {
                                var substitutes = targetMapping.Substitute.Split(',');

                                foreach (var substitute in substitutes)
                                {
                                    var dataTypeSpec = GetDataTypeSpecification(targetDataTypeSpecs, substitute.Trim());

                                    if (dataTypeSpec != null)
                                    {
                                        if (string.IsNullOrEmpty(dataTypeSpec.Args))
                                        {
                                            dataTypeInfo.DataType = substitute;
                                            break;
                                        }

                                        var range = DataTypeManager.GetArgumentRange(dataTypeSpec, "length");

                                        if (range.HasValue && range.Value.Max >= dataTypeInfo.MaxLength)
                                        {
                                            dataTypeInfo.DataType = substitute;
                                            break;
                                        }
                                    }
                                }
                            }
                    }
                }
                else
                {
                    if (dataTypeMapping.Specials != null && dataTypeMapping.Specials.Count > 0)
                        foreach (var special in dataTypeMapping.Specials)
                        {
                            var name = special.Name;
                            var matched = false;

                            if (name == "maxLength")
                                matched = IsSpecialMaxLengthMatched(special, dataTypeInfo);
                            else if (name == "precisionScale")
                                matched = IsSpecialPrecisionAndScaleMatched(special, dataTypeInfo);
                            else if (name.Contains("precision") || name.Contains("scale"))
                                matched = IsSpecialPrecisionOrScaleMatched(special, dataTypeInfo);
                            else if (name == "expression")
                                matched = IsSpecialExpressionMatched(special, originalDataType);
                            else if (name == "isIdentity") matched = dataTypeInfo.IsIdentity;


                            if (matched) dataTypeInfo.DataType = special.Type;

                            if (!string.IsNullOrEmpty(special.TargetMaxLength))
                                dataTypeInfo.MaxLength = int.Parse(special.TargetMaxLength);
                        }

                    if (string.IsNullOrEmpty(targetDataTypeSpec.Format))
                    {
                        var useConfigPrecisionScale = false;

                        if (!string.IsNullOrEmpty(targetMapping.Precision))
                        {
                            dataTypeInfo.Precision = int.Parse(targetMapping.Precision);

                            useConfigPrecisionScale = true;
                        }

                        if (!string.IsNullOrEmpty(targetMapping.Scale))
                        {
                            dataTypeInfo.Scale = int.Parse(targetMapping.Scale);

                            useConfigPrecisionScale = true;
                        }

                        if (!useConfigPrecisionScale)
                        {
                            if (sourceDataTypeSpec.Args == targetDataTypeSpec.Args)
                            {
                                var precisionRange = DataTypeManager.GetArgumentRange(targetDataTypeSpec, "precision");
                                var scaleRange = DataTypeManager.GetArgumentRange(targetDataTypeSpec, "scale");

                                if (precisionRange.HasValue && dataTypeInfo.Precision > precisionRange.Value.Max)
                                    dataTypeInfo.Precision = precisionRange.Value.Max;

                                if (scaleRange.HasValue && dataTypeInfo.Scale > scaleRange.Value.Max)
                                    dataTypeInfo.Scale = scaleRange.Value.Max;

                                if (dataTypeInfo.Precision.HasValue)
                                    if (dataTypeInfo.DataType.ToLower() == "int")
                                        if (dataTypeInfo.Precision.Value > 10)
                                            dataTypeInfo.DataType = "bigint";
                            }
                            else
                            {
                                var defaultValues = targetDataTypeSpec.Default?.Split(',');

                                var hasDefaultValues = defaultValues != null && defaultValues.Length > 0;

                                var args = targetDataTypeSpec.Args;

                                if (hasDefaultValues)
                                {
                                    if (args == "precision,scale" && defaultValues.Length == 2)
                                    {
                                        dataTypeInfo.Precision = int.Parse(defaultValues[0]);
                                        dataTypeInfo.Scale = int.Parse(defaultValues[1]);
                                    }
                                    else if (args == "scale" && defaultValues.Length == 1)
                                    {
                                        dataTypeInfo.Scale = int.Parse(defaultValues[0]);
                                    }
                                }
                                else
                                {
                                    dataTypeInfo.Precision = default(int?);
                                }
                            }
                        }
                    }
                    else
                    {
                        var format = targetDataTypeSpec.Format;
                        var dataType = format;

                        var defaultValues = targetDataTypeSpec.Default?.Split(',');
                        var targetMappingArgs = targetMapping.Args;

                        var i = 0;
                        foreach (var arg in targetDataTypeSpec.Arguments)
                        {
                            if (arg.Name.ToLower() == "scale")
                            {
                                var targetScaleRange = DataTypeManager.GetArgumentRange(targetDataTypeSpec, "scale");

                                var scale = dataTypeInfo.Scale == null ? 0 : dataTypeInfo.Scale.Value;

                                if (targetScaleRange.HasValue && scale > targetScaleRange.Value.Max)
                                    scale = targetScaleRange.Value.Max;

                                dataType = dataType.Replace("$scale$", scale.ToString());
                            }
                            else
                            {
                                var defaultValue = defaultValues != null && defaultValues.Length > i
                                    ? defaultValues[i]
                                    : "";

                                var value = defaultValue;

                                if (targetMapping.Arguments.Any(item => item.Name == arg.Name))
                                    value = targetMapping.Arguments.FirstOrDefault(item => item.Name == arg.Name).Value;

                                dataType = dataType.Replace($"${arg.Name}$", value);
                            }

                            i++;
                        }

                        dataTypeInfo.DataType = dataType;
                    }
                }
            }
            else
            {
                dataTypeInfo.DataType = targetDbInterpreter.DefaultDataType;
            }
        }

        private static readonly Regex GetDataTypeSpecificationRegex = new Regex(@"([(][^(^)]+[)])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static DataTypeSpecification GetDataTypeSpecification(
            IEnumerable<DataTypeSpecification> dataTypeSpecifications, string dataType)
        {
            var regex = GetDataTypeSpecificationRegex;

            if (regex.IsMatch(dataType))
            {
                var matches = regex.Matches(dataType);

                foreach (Match match in matches) dataType = regex.Replace(dataType, "");
            }

            return dataTypeSpecifications.FirstOrDefault(item => item.Name.ToLower() == dataType.ToLower().Trim());
        }

        private static bool IsSpecialMaxLengthMatched(DataTypeMappingSpecial special, DataTypeInfo dataTypeInfo)
        {
            var value = special.Value;

            if (string.IsNullOrEmpty(value)) return false;

            if (value == dataTypeInfo.MaxLength?.ToString()) return true;

            if (dataTypeInfo.MaxLength.HasValue && (value.StartsWith(">") || value.StartsWith("<")))
            {
                var exp = new Expression($"{dataTypeInfo.MaxLength}{value}");

                if (!exp.HasErrors())
                {
                    var result = exp.Evaluate();

                    return result != null && result is bool && (bool)result;
                }
            }

            return false;
        }

        private static bool IsSpecialPrecisionOrScaleMatched(DataTypeMappingSpecial special, DataTypeInfo dataTypeInfo)
        {
            var names = special.Name.Split(',');
            var values = special.Value.Split(',');

            string precision = null;
            string scale = null;

            var i = 0;
            foreach (var name in names)
            {
                if (name == "precision")
                    precision = values[i];
                else if (name == "scale") scale = values[i];

                i++;
            }

            if (!string.IsNullOrEmpty(precision) && !string.IsNullOrEmpty(scale))
                return IsValueEqual(precision, dataTypeInfo.Precision) && IsValueEqual(scale, dataTypeInfo.Scale);
            if (!string.IsNullOrEmpty(precision) && string.IsNullOrEmpty(scale))
                return IsValueEqual(precision, dataTypeInfo.Precision);
            if (string.IsNullOrEmpty(precision) && !string.IsNullOrEmpty(scale))
                return IsValueEqual(scale, dataTypeInfo.Scale);
            return false;
        }

        private static bool IsSpecialPrecisionAndScaleMatched(DataTypeMappingSpecial special, DataTypeInfo dataTypeInfo)
        {
            var precision = special.Precison;
            var scale = special.Scale;

            return precision == "isNullOrZero" && IsNullOrZero(dataTypeInfo.Precision)
                                               && scale == "isNullOrZero" && IsNullOrZero(dataTypeInfo.Scale);
        }

        private static bool IsNullOrZero(long? value)
        {
            return value == null || value == 0;
        }

        private static bool IsValueEqual(string value1, long? value2)
        {
            var v2 = value2?.ToString();
            if (value1 == v2)
                return true;
            return value1 == "0" && (v2 is null || v2 == "");
        }

        private static bool IsSpecialExpressionMatched(DataTypeMappingSpecial special, string dataType)
        {
            var value = special.Value;

            return Regex.IsMatch(dataType, value, RegexOptions.IgnoreCase);
        }
    }
}