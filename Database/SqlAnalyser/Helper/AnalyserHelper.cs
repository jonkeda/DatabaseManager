using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public static class AnalyserHelper
    {
        public static char[] QuotationChars = { '[', ']', '"', '`' };
        public static char[] TrimChars = { '@', '[', ']', '"', '`', ':' };

        public static bool HasWord(string value, string word, int? startIndex = null, int? endIndex = null)
        {
            var start = startIndex.HasValue ? startIndex.Value : 0;

            var content = endIndex.HasValue && endIndex.Value > 0 ? value.Substring(start, endIndex.Value) : value;

            return Regex.IsMatch(content, $@"\b({word})\b", RegexOptions.IgnoreCase);
        }

        public static bool IsSubQuery(ParserRuleContext node)
        {
            if (node != null)
                return IsSubQuery(node.GetText());
            return false;
        }

        public static bool IsSubQuery(string query)
        {
            var hasWord = HasWord(query, "SELECT");

            if (hasWord)
            {
                var index = query.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);

                var singleQuotationCharCount = query.Substring(0, index).Count(item => item == '\'');

                if (singleQuotationCharCount % 2 == 0) return true;
            }

            return false;
        }

        public static string ReplaceSymbol(string symbol, string oldValue, string newValue)
        {
            string pattern;
            if (RegexHelper.NameRegex.IsMatch(oldValue))
                pattern = $"\\b{oldValue}\\b";
            else
                pattern = $"({oldValue})";

            return Regex.Replace(symbol, pattern, newValue, RegexOptions.Multiline);
        }

        public static bool IsFromItemsHaveJoin(List<FromItem> fromItems)
        {
            return fromItems != null && fromItems.Count > 0 &&
                   fromItems.Any(item => item.JoinItems != null && item.JoinItems.Count > 0);
        }

        public static bool HasExitStatement(WhileStatement statement)
        {
            var types = new[] { typeof(BreakStatement), typeof(LoopExitStatement) };

            foreach (var st in statement.Statements)
                if (types.Contains(st.GetType()))
                    return true;
                else if (st is IfStatement @if)
                    foreach (var item in @if.Items)
                    {
                        foreach (var ist in item.Statements)
                            if (types.Contains(ist.GetType()))
                                return true;
                            else if (ist is WhileStatement @while) return HasExitStatement(@while);
                    }
                else if (st is WhileStatement @while) return HasExitStatement(@while);

            return false;
        }

        public static bool IsAssignNameColumn(ColumnName column)
        {
            var symbol = column.Symbol;

            if (symbol.Contains("="))
            {
                var items = symbol.Split('=');

                var assignName = items[0].Trim(TrimChars).Trim();

                if (RegexHelper.NameRegex.IsMatch(assignName)) return true;
            }

            return false;
        }

        public static bool IsValidColumnName(TokenInfo token)
        {
            var symbol = token.Symbol;

            if (string.IsNullOrEmpty(symbol)) return false;

            var items = symbol.Split('.');

            return items.All(item => IsValidName(item));
        }

        public static bool IsValidName(string name)
        {
            var trimmedName = name.Trim(TrimChars).Trim();

            if (trimmedName.Contains(" ") && IsNameQuoted(name.Trim()))
                return true;
            return RegexHelper.NameRegex.IsMatch(trimmedName);
        }

        public static bool IsNameQuoted(string name)
        {
            return QuotationChars.Any(item => name.StartsWith(item.ToString()) && name.EndsWith(item.ToString()));
        }

        public static TokenInfo GetIntoTableName(SelectStatement statement)
        {
            if (statement.Intos == null || statement.Intos.Count == 0) return null;

            return statement.Intos.FirstOrDefault(item => item.Type == TokenType.TableName);
        }

        public static string GetUserVariableDataType(DatabaseType databaseType, UserVariableDataType dataType)
        {
            switch (databaseType)
            {
                case DatabaseType.SqlServer:
                    switch (dataType)
                    {
                        case UserVariableDataType.String:
                            return "VARCHAR(MAX)";
                        case UserVariableDataType.Integer:
                            return "INT";
                        case UserVariableDataType.Decimal:
                            return "FLOAT";
                    }

                    break;
                case DatabaseType.Postgres:
                    switch (dataType)
                    {
                        case UserVariableDataType.String:
                            return "CHARACTER VARYING";
                        case UserVariableDataType.Integer:
                            return "INTEGER";
                        case UserVariableDataType.Decimal:
                            return "DOUBLE PRECISION";
                    }

                    break;
                case DatabaseType.Oracle:
                    switch (dataType)
                    {
                        case UserVariableDataType.String:
                            return "VARCHAR2(4000)";
                        case UserVariableDataType.Integer:
                            return "NUMBER";
                        case UserVariableDataType.Decimal:
                            return "FLOAT";
                    }

                    break;
            }

            return string.Empty;
        }
    }
}