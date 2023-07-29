using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Databases.Handlers;
using DatabaseConverter.Core.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Core;
using SqlAnalyser.Model;

namespace DatabaseConverter.Core
{
    public class TranslateHelper
    {
        public static string ConvertNumberToPostgresMoney(string str)
        {
            var matches = RegexHelper.NumberRegex.Matches(str);

            if (matches != null)
                foreach (Match match in matches)
                    if (!string.IsNullOrEmpty(match.Value))
                        str = str.Replace(match.Value, $"{match.Value}::money");

            return str;
        }

        public static string RemovePostgresDataTypeConvertExpression(string value,
            IEnumerable<DataTypeSpecification> dataTypeSpecifications, char quotationLeftChar, char quotationRightChar)
        {
            if (value.Contains("::")) //datatype convert operator
            {
                var specs = dataTypeSpecifications.Where(item =>
                    value.ToLower().Contains($"::{item.Name}") ||
                    value.ToLower().Contains($"{quotationLeftChar}{item.Name}{quotationRightChar}")
                ).OrderByDescending(item => item.Name.Length);

                if (specs != null)
                    foreach (var spec in specs)
                        value = value.Replace($"::{quotationLeftChar}{spec.Name}{quotationRightChar}", "")
                            .Replace($"::{spec.Name}", "");
            }

            return value;
        }

        public static IEnumerable<char> GetTrimChars(params DbInterpreter[] dbInterpreters)
        {
            foreach (var interpreter in dbInterpreters)
            {
                yield return interpreter.QuotationLeftChar;
                yield return interpreter.QuotationRightChar;
            }
        }

        public static string ExtractNameFromParenthesis(string value)
        {
            if (value != null)
            {
                var index = value.IndexOf("(", StringComparison.Ordinal);

                if (index > 0) return value.Substring(0, index).Trim();
            }

            return value;
        }

        public static TableColumn SimulateTableColumn(DbInterpreter dbInterpreter, string dataType,
            DbConverterOption option, List<UserDefinedType> userDefinedTypes, char[] trimChars)
        {
            var column = new TableColumn();

            if (userDefinedTypes != null && userDefinedTypes.Count > 0)
            {
                var userDefinedType = userDefinedTypes.FirstOrDefault(item =>
                    item.Name.Trim(trimChars).ToUpper() == dataType.Trim(trimChars).ToUpper());

                if (userDefinedType != null)
                {
                    var attr = userDefinedType.Attributes.First();

                    column.DataType = attr.DataType;
                    column.MaxLength = attr.MaxLength;

                    return column;
                }
            }

            var dataTypeInfo = dbInterpreter.GetDataTypeInfo(dataType);

            var dataTypeName = dataTypeInfo.DataType.Trim().ToLower();

            column.DataType = dataTypeName;

            var isChar = DataTypeHelper.IsCharType(dataTypeName);
            var isBinary = DataTypeHelper.IsBinaryType(dataTypeName);

            var args = dataTypeInfo.Args;
            var precision = default(int?);
            var scale = default(int?);
            var maxLength = -1;

            if (!string.IsNullOrEmpty(args))
            {
                var argItems = args.Split(',');

                if (isChar || isBinary)
                {
                    maxLength = GetDataTypeArgumentValue(argItems[0].Trim(), true).Value;

                    if (isChar && DataTypeHelper.StartsWithN(dataTypeName) && maxLength > 0) maxLength *= 2;
                }
                else
                {
                    var value = -1;

                    if (int.TryParse(argItems[0], out value))
                        if (value > 0)
                        {
                            var dataTypeSpecification = dbInterpreter.GetDataTypeSpecification(dataTypeName);

                            if (dataTypeSpecification != null)
                            {
                                var specArgs = dataTypeSpecification.Args;

                                if (specArgs == "scale")
                                {
                                    scale = GetDataTypeArgumentValue(argItems[0]);
                                }
                                else if (specArgs == "precision,scale")
                                {
                                    precision = GetDataTypeArgumentValue(argItems[0]);

                                    if (argItems.Length > 1) scale = GetDataTypeArgumentValue(argItems[1]);
                                }
                            }
                        }
                }
            }

            column.Precision = precision;
            column.Scale = scale;
            column.MaxLength = maxLength;

            return column;
        }

        private static int? GetDataTypeArgumentValue(string value, bool isChar = false)
        {
            var intValue = -1;

            if (int.TryParse(value, out intValue)) return intValue;

            return isChar ? -1 : default(int?);
        }

        public static void TranslateTableColumnDataType(DataTypeTranslator dataTypeTranslator, TableColumn column)
        {
            var dataTypeInfo = DataTypeHelper.GetDataTypeInfoByTableColumn(column);

            dataTypeTranslator.Translate(dataTypeInfo);

            DataTypeHelper.SetDataTypeInfoToTableColumn(dataTypeInfo, column);
        }

        public static void RestoreTokenValue(string definition, TokenInfo token)
        {
            if (token != null && token.StartIndex.HasValue && token.Length > 0)
                token.Symbol = definition.Substring(token.StartIndex.Value, token.Length);
        }

        public static SqlAnalyserBase GetSqlAnalyser(DatabaseType databaseType, string content)
        {
            return SqlHandler.GetHandler(databaseType).GetSqlAnalyser(content);
        }

        public static ScriptBuildFactory GetScriptBuildFactory(DatabaseType databaseType)
        {
            return SqlHandler.GetHandler(databaseType).CreateScriptBuildFactory();
        }

        public static string TranslateComments(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter,
            string value)
        {
            var sb = new StringBuilder();

            var lines = value.Split('\n');

            foreach (var line in lines)
            {
                var index = line.IndexOf(sourceDbInterpreter.CommentString, StringComparison.Ordinal);
                var handled = false;

                if (index >= 0)
                {
                    var singleQuotationCharCount = line.Substring(0, index).Count(item => item == '\'');

                    if (singleQuotationCharCount % 2 == 0)
                    {
                        sb.Append(
                            $"{line.Substring(0, index)}{targetDbInterpreter.CommentString}{line.Substring(index + 2)}");

                        handled = true;
                    }
                }

                if (!handled) sb.Append(line);

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static bool NeedConvertConcatChar(List<string> databaseTypes, DatabaseType databaseType)
        {
            return databaseTypes.Count == 0 || databaseTypes.Any(item => item == databaseType.ToString());
        }

        public static bool IsValidName(string name, char[] trimChars)
        {
            var trimedName = name.Trim().Trim(trimChars).Trim();

            if (trimedName.Contains(" ") && IsNameQuoted(name.Trim(), trimChars))
                return true;
            return RegexHelper.NameRegex.IsMatch(trimedName);
        }

        public static bool IsNameQuoted(string name, char[] trimChars)
        {
            return trimChars.Any(item => name.StartsWith(item.ToString()) && name.EndsWith(item.ToString()));
        }

        public static bool IsAssignClause(string value, char[] trimChars)
        {
            if (value.Contains("="))
            {
                var items = value.Split('=');

                var assignName = items[0].Trim(trimChars).Trim();

                if (RegexHelper.NameRegex.IsMatch(assignName)) return true;
            }

            return false;
        }
    }
}