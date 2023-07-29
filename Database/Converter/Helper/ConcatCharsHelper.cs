using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public static class StringExtensions
    {
        public static bool Contains(this string str, string value, StringComparison stringComparison)
        {
            return str.IndexOf(value, stringComparison) >= 0;
        }

        // todo check this if it works the same as Split by string in .net 2.1
        public static string[] SplitByString(this string str, string delimiter)
        {
            return str.Split(new[] { delimiter }, StringSplitOptions.None);
        }

        public static string[] SplitByString(this string str, string delimiter, StringSplitOptions stringSplitOptions)
        {
            return str.Split(new[] { delimiter }, stringSplitOptions);
        }

        public static string ReplaceOrdinalIgnoreCase(this string str, string oldValue, string newValue)
        {
            return Regex.Replace(str, Regex.Escape(oldValue), newValue, RegexOptions.IgnoreCase);
        }
    }

    public class ConcatCharsHelper
    {
        public const string KEYWORDS = "AND|NOT|NULL|IS|CASE|WHEN|THEN|ELSE|END|LIKE|FOR|IN|OR";

        public static string ConvertConcatChars(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter,
            string symbol, IEnumerable<string> charItems = null)
        {
            var sourceConcatChars = sourceDbInterpreter.STR_CONCAT_CHARS;
            var targetConcatChars = targetDbInterpreter.STR_CONCAT_CHARS;

            if (!symbol.Contains(sourceConcatChars))
            {
                return symbol;
            }

            var quotationChars = TranslateHelper.GetTrimChars(sourceDbInterpreter, targetDbInterpreter).ToArray();
            var hasParenthesis = symbol.Trim().StartsWith("(");

            var targetDbType = targetDbInterpreter.DatabaseType;

            var items = SplitByKeywords(StringHelper.GetBalanceParenthesisTrimedValue(symbol));

            var sb = new StringBuilder();

            foreach (var item in items)
            {
                if (item.Index > 0)
                {
                    sb.Append(" ");
                }

                if (item.Type == TokenSymbolItemType.Keyword || item.Content.Trim().Length == 0)
                {
                    sb.Append(item.Content);
                }
                else
                {
                    var res = InternalConvertConcatChars(sourceDbInterpreter, targetDbInterpreter, item.Content,
                        charItems);

                    sb.Append(res);
                }
            }

            var result = sb.ToString();

            if (StringHelper.IsParenthesisBalanced(result))
            {
                if (result.Contains(sourceConcatChars)) //check the final result
                {
                    sb.Clear();

                    if (!string.IsNullOrEmpty(targetConcatChars))
                    {
                        var symbolItems = SplitByConcatChars(StringHelper.GetBalanceParenthesisTrimedValue(result),
                            sourceConcatChars);

                        for (var i = 0; i < symbolItems.Count; i++)
                        {
                            var content = symbolItems[i].Content;

                            sb.Append(content);

                            if (i < symbolItems.Count - 1)
                            {
                                var nextContent = i + 1 <= symbolItems.Count - 1 ? symbolItems[i + 1].Content : null;

                                var currentContentIsMatched =
                                    IsStringValueMatched(content, targetDbType, quotationChars, charItems);

                                var nextContentIsMatched = nextContent != null &&
                                                           IsStringValueMatched(nextContent, targetDbType,
                                                               quotationChars, charItems);

                                if (currentContentIsMatched || nextContentIsMatched)
                                {
                                    sb.Append(targetConcatChars);
                                }
                                else
                                {
                                    sb.Append(sourceConcatChars);
                                }
                            }
                        }
                    }
                    else
                    {
                        var isAssignClause = TranslateHelper.IsAssignClause(result, quotationChars);
                        var strAssign = "";
                        var parseContent = result;

                        if (isAssignClause)
                        {
                            var equalMarkIndex = result.IndexOf("=", StringComparison.Ordinal);

                            strAssign = result.Substring(0, equalMarkIndex + 1);
                            parseContent = result.Substring(equalMarkIndex + 1);
                        }

                        var symbolItems =
                            SplitByConcatChars(StringHelper.GetBalanceParenthesisTrimedValue(parseContent),
                                sourceConcatChars);

                        var hasStringValue = false;

                        for (var i = 0; i < symbolItems.Count; i++)
                        {
                            var content = symbolItems[i].Content;

                            var isMatched = IsStringValueMatched(content, targetDbType, quotationChars, charItems);

                            if (isMatched)
                            {
                                hasStringValue = true;
                            }
                            else
                            {
                                var upperContent = content.Trim('(', ')').Trim().ToUpper();

                                if (upperContent.StartsWith("CASE") && upperContent.EndsWith("END"))
                                {
                                    var subItems = SplitByConcatChars(upperContent, " ");

                                    hasStringValue = subItems.Any(item =>
                                        IsStringValueMatched(item.Content, targetDbType, quotationChars, charItems));
                                }
                            }

                            if (hasStringValue)
                            {
                                break;
                            }
                        }

                        if (hasStringValue)
                        {
                            sb.Append(
                                $"{strAssign}CONCAT({string.Join(",", symbolItems.Select(item => item.Content))})");
                        }
                        else
                        {
                            sb.Append(result);
                        }
                    }

                    result = sb.ToString();
                }

                return GetOriginalValue(result, hasParenthesis);
            }

            return symbol;
        }

        private static string GetOriginalValue(string value, bool hasParenthesis)
        {
            if (!hasParenthesis)
            {
                return value;
            }

            if (!value.Trim().StartsWith("("))
            {
                return $"({value})";
            }

            return value;
        }

        private static string InternalConvertConcatChars(DbInterpreter sourceDbInterpreter,
            DbInterpreter targetDbInterpreter, string symbol, IEnumerable<string> charItems = null)
        {
            var sourceConcatChars = sourceDbInterpreter.STR_CONCAT_CHARS;
            var targetConcatChars = targetDbInterpreter.STR_CONCAT_CHARS;

            if (!symbol.Contains(sourceConcatChars))
            {
                return symbol;
            }

            var sourceDbType = sourceDbInterpreter.DatabaseType;
            var targetDbType = targetDbInterpreter.DatabaseType;

            var hasParenthesis = symbol.Trim().StartsWith("(");

            var quotationChars = TranslateHelper.GetTrimChars(sourceDbInterpreter, targetDbInterpreter).ToArray();

            var symbolItems =
                SplitByConcatChars(StringHelper.GetBalanceParenthesisTrimedValue(symbol), sourceConcatChars);

            if (symbolItems.Count == 1)
            {
                var content = symbolItems[0].Content;

                if (!content.Contains("("))
                {
                    return symbol;
                }

                var equalMarkIndex = content.IndexOf("=", StringComparison.Ordinal);
                var parenthesisIndex = content.IndexOf("(", StringComparison.Ordinal);

                var assignName = "";
                var functionName = "";

                if (equalMarkIndex >= 0 && equalMarkIndex < parenthesisIndex)
                {
                    assignName = content.Substring(0, equalMarkIndex);
                    functionName = content.Substring(equalMarkIndex + 1, parenthesisIndex - equalMarkIndex - 1);
                }
                else
                {
                    functionName = content.Substring(0, parenthesisIndex);
                }

                var spec = FunctionManager.GetFunctionSpecifications(targetDbType)
                    .FirstOrDefault(item => item.Name.ToUpper() == functionName.Trim().ToUpper());

                if (spec == null) //if no target function specification, use the source instead.
                {
                    spec = FunctionManager.GetFunctionSpecifications(sourceDbType)
                        .FirstOrDefault(item => item.Name.ToUpper() == functionName.Trim().ToUpper());
                }

                if (spec != null)
                {
                    var formula = new FunctionFormula(content);

                    if (formula != null)
                    {
                        var args = formula.GetArgs(spec.Delimiter ?? ",");

                        var results = new List<string>();

                        foreach (var arg in args)
                        {
                            results.Add(ConvertConcatChars(sourceDbInterpreter, targetDbInterpreter, arg, charItems));
                        }

                        var delimiter = spec.Delimiter == "," ? "," : $" {spec.Delimiter} ";
                        var strAssign = !string.IsNullOrEmpty(assignName) ? assignName + "=" : "";

                        return $"{strAssign}{functionName}({string.Join(delimiter, results)})";
                    }
                }

                return GetOriginalValue(content, hasParenthesis);
            }

            foreach (var item in symbolItems)
            {
                if (item.Content != symbol && item.Content.Contains("("))
                {
                    item.Content =
                        ConvertConcatChars(sourceDbInterpreter, targetDbInterpreter, item.Content, charItems);
                }
            }

            if (sourceConcatChars == "+")
            {
                if (symbolItems.Any(item => decimal.TryParse(item.Content.Trim(',', ' '), out _)))
                {
                    return symbol;
                }
            }

            Func<string, bool> isMatched = value =>
            {
                return IsStringValueMatched(value, targetDbType, quotationChars, charItems);
            };

            var hasStringValue = symbolItems.Any(item => isMatched(item.Content.Trim()));

            var sb = new StringBuilder();

            if (hasStringValue)
            {
                var items = symbolItems.Select(item => item.Content).ToArray();

                if (!string.IsNullOrEmpty(targetConcatChars))
                {
                    sb.Append(string.Join(targetConcatChars, items));
                }
                else
                {
                    var hasInvalid = false;

                    for (var i = 0; i < items.Length; i++)
                    {
                        var value = items[i];

                        if (isMatched(value.Trim()))
                        {
                            sb.Append(value);
                        }
                        else
                        {
                            if (value.StartsWith("@")
                                || RegexHelper.NameRegex.IsMatch(value.Trim('(', ')', ' '))
                                || (value.Contains(".") && value.Split('.').All(item =>
                                    RegexHelper.NameRegex.IsMatch(item.Trim().Trim(quotationChars))))
                                || RegexHelper.NameRegex.IsMatch(
                                    TranslateHelper.ExtractNameFromParenthesis(value.Trim()))
                               )
                            {
                                sb.Append(value);
                            }
                            else
                            {
                                hasInvalid = true;
                                break;
                            }
                        }

                        if (i < items.Length - 1)
                        {
                            sb.Append(",");
                        }
                    }

                    if (!hasInvalid)
                    {
                        sb.Insert(0, "CONCAT(");
                        sb.Append(")");
                    }
                    else
                    {
                        return symbol;
                    }
                }
            }

            if (symbol.Trim().EndsWith(sourceConcatChars))
            {
                if (!string.IsNullOrEmpty(targetConcatChars))
                {
                    sb.Append(targetConcatChars);
                }
                else
                {
                    sb.Append(sourceConcatChars);
                }
            }

            var result = sb.ToString().Trim();

            if (result.Length > 0 && StringHelper.IsParenthesisBalanced(result))
            {
                return GetOriginalValue(result, hasParenthesis);
            }

            return symbol;
        }

        private static bool IsStringValueMatched(string value, DatabaseType targetDbType, char[] quotationChars,
            IEnumerable<string> charItems = null)
        {
            return ValueHelper.IsStringValue(value.Trim('(', ')'))
                   || IsStringFunction(targetDbType, value)
                   || (charItems != null && charItems.Any(c => c.Trim(quotationChars) == value.Trim(quotationChars)));
        }

        private static List<TokenSymbolItemInfo> SplitByKeywords(string value)
        {
            var keywordsRegex = $@"\b({KEYWORDS})\b";

            var matches = Regex.Matches(value, keywordsRegex, RegexOptions.IgnoreCase);

            var tokenSymbolItems = new List<TokenSymbolItemInfo>();

            Action<string, TokenSymbolItemType> addItem = (content, type) =>
            {
                var contentItem = new TokenSymbolItemInfo
                {
                    Index = tokenSymbolItems.Count(),
                    Content = content,
                    Type = type
                };

                tokenSymbolItems.Add(contentItem);
            };

            var matchList = new List<Match>();

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];

                if (match.Index > 0)
                {
                    if ("@" + match.Value == value.Substring(match.Index - 1, match.Length + 1))
                    {
                        continue;
                    }

                    var singleQuotationCharCount = value.Substring(0, match.Index).Count(item => item == '\'');

                    if (singleQuotationCharCount % 2 != 0)
                    {
                        continue;
                    }
                }

                matchList.Add(match);
            }

            for (var i = 0; i < matchList.Count; i++)
            {
                var match = matchList[i];

                var index = match.Index;

                if (index > 0)
                {
                    if ("@" + match.Value == value.Substring(match.Index - 1, match.Length + 1))
                    {
                        continue;
                    }
                }

                var singleQuotaionCharCount = value.Substring(0, index).Count(item => item == '\'');

                if (singleQuotaionCharCount % 2 == 0)
                {
                    if (i == 0 && match.Index > 0)
                    {
                        addItem(value.Substring(0, match.Index), TokenSymbolItemType.Content);
                    }

                    addItem(match.Value, TokenSymbolItemType.Keyword);

                    if (i < matchList.Count - 1)
                    {
                        var nextMatchIndex = matchList[i + 1].Index;

                        var content = value.Substring(index + match.Length, nextMatchIndex - index - match.Length);

                        addItem(content, TokenSymbolItemType.Content);
                    }
                    else if (i == matchList.Count - 1)
                    {
                        var startIndex = match.Index + match.Length;

                        if (startIndex < value.Length)
                        {
                            var content = value.Substring(startIndex);

                            addItem(content, TokenSymbolItemType.Content);
                        }
                    }
                }
            }

            if (tokenSymbolItems.Count == 0)
            {
                addItem(value, TokenSymbolItemType.Content);
            }

            return tokenSymbolItems;
        }

        private static bool IsStringFunction(DatabaseType databaseType, string value)
        {
            var functionName = TranslateHelper.ExtractNameFromParenthesis(value.Trim()).ToUpper();
            var specification = FunctionManager.GetFunctionSpecifications(databaseType)
                .FirstOrDefault(item => item.Name.ToUpper() == functionName);

            if (specification != null)
            {
                if (specification.IsString)
                {
                    return true;
                }

                return IsStringConvertFunction(databaseType, specification, value);
            }

            return false;
        }

        private static bool IsStringConvertFunction(DatabaseType databaseType, FunctionSpecification specification,
            string value)
        {
            var name = specification.Name.Trim().ToUpper();

            if (name != "CONVERT" && name != "CAST")
            {
                return false;
            }

            if (!string.IsNullOrEmpty(specification.Args))
            {
                var index = value.IndexOf("(", StringComparison.Ordinal);

                var content = StringHelper.GetBalanceParenthesisTrimedValue(value.Substring(index));

                var items = content.SplitByString(specification.Delimiter ?? ",");

                string dataType = null;

                if (name == "CONVERT")
                {
                    if (databaseType == DatabaseType.MySql)
                    {
                        dataType = items.LastOrDefault()?.Trim();
                    }
                    else
                    {
                        dataType = items.FirstOrDefault()?.Trim();
                    }
                }
                else if (name == "CAST")
                {
                    dataType = items.LastOrDefault()?.Trim();
                }

                if (dataType != null && DataTypeHelper.IsCharType(dataType))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<TokenSymbolItemInfo> SplitByConcatChars(string value, string concatChars)
        {
            var items = new List<TokenSymbolItemInfo>();

            var concatFirstChar = concatChars.First();
            var concatCharsLength = concatChars.Length;
            var singleQuotationCharCount = 0;
            var leftParenthesisCount = 0;
            var rightParenthesisCount = 0;

            var sb = new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '\'')
                {
                    singleQuotationCharCount++;
                }
                else if (c == '(')
                {
                    if (singleQuotationCharCount % 2 == 0)
                    {
                        leftParenthesisCount++;
                    }
                }
                else if (c == ')')
                {
                    if (singleQuotationCharCount % 2 == 0)
                    {
                        rightParenthesisCount++;
                    }
                }

                if (c == concatFirstChar)
                {
                    var fowardChars = concatCharsLength == 1 ? concatChars
                        : i + concatCharsLength <= value.Length - 1 ? value.Substring(i, concatCharsLength)
                        : value.Substring(i);

                    if (fowardChars == concatChars)
                    {
                        if (singleQuotationCharCount % 2 == 0 && leftParenthesisCount == rightParenthesisCount)
                        {
                            var item = new TokenSymbolItemInfo
                            {
                                Content = sb.ToString(),
                                Index = items.Count
                            };

                            items.Add(item);

                            i = i + concatCharsLength - 1;

                            sb.Clear();

                            continue;
                        }
                    }
                }

                sb.Append(c);
            }

            if (sb.Length > 0) //last one
            {
                var item = new TokenSymbolItemInfo
                {
                    Content = sb.ToString(),
                    Index = items.Count
                };

                items.Add(item);
            }

            return items;
        }
    }
}