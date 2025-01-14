﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatabaseInterpreter.Utility
{
    public class StringHelper
    {
        public static string GetSingleQuotedString(params string[] values)
        {
            if (values != null) return string.Join(",", values.Select(item => $"'{item}'"));
            return null;
        }

        public static bool IsStartWithSingleQuotationChar(string value)
        {
            return value.StartsWith("'") || value.StartsWith("N'", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEndWithSingleQuotationChar(string value)
        {
            return value.EndsWith("'");
        }

        public static string RemoveEmoji(string str)
        {
            return Regex.Replace(str, @"\p{Cs}", "");
        }

        public static string RawToGuid(string text)
        {
            var bytes = ParseHex(text);
            var guid = new Guid(bytes);
            return guid.ToString("N").ToUpperInvariant();
        }

        public static string GuidToRaw(string text)
        {
            var guid = new Guid(text);
            return BitConverter.ToString(guid.ToByteArray()).Replace("-", "");
        }

        public static byte[] ParseHex(string text)
        {
            var ret = new byte[text.Length / 2];
            for (var i = 0; i < ret.Length; i++) ret[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return ret;
        }

        public static string ToSingleEmptyLine(string value)
        {
            if (value != null)
                return Regex.Replace(value, "(\\r\\n){3,}", Environment.NewLine + Environment.NewLine,
                    RegexOptions.Multiline);
            return value;
        }

        public static string GetFriendlyTypeName(string name)
        {
            var reg = new Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            return reg.Replace(name, " ");
        }

        public static string FormatScript(string scripts)
        {
            var regex = new Regex(@"([;]+[\s]*[;]+)|(\r\n[\s]*[;])");

            return ToSingleEmptyLine(regex.Replace(scripts, ";"));
        }

        public static string HandleSingleQuotationChar(string value)
        {
            return value.Replace("'", "''");
        }

        public static string TrimParenthesis(string value)
        {
            while (value.Length > 2 && value.StartsWith("(") && value.EndsWith(")"))
                value = value.Substring(1, value.Length - 2);

            return value;
        }

        public static string GetParenthesisedString(string value)
        {
            if (IsParenthesised(value)) return value;

            return $"({value})";
        }

        public static bool IsParenthesised(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            var trimedValue = value.Trim();

            return trimedValue.StartsWith("(") && trimedValue.EndsWith(")") && IsParenthesisBalanced(trimedValue);
        }

        public static string GetBalanceParenthesisTrimedValue(string value)
        {
            if (!string.IsNullOrEmpty(value) && value.StartsWith("(") && value.EndsWith(")"))
            {
                while (value.StartsWith("(") && value.EndsWith(")") && IsParenthesisBalanced(value))
                {
                    var trimedValue = value.Substring(1, value.Length - 2);

                    if (!IsParenthesisBalanced(trimedValue))
                        return value;
                    value = trimedValue;
                }

                return value.Trim();
            }

            return value?.Trim();
        }

        public static bool IsParenthesisBalanced(string value)
        {
            if (string.IsNullOrEmpty(value) || (!value.Contains("(") && !value.Contains(")"))) return true;

            var pairs = new Dictionary<char, char> { { '(', ')' } };

            var parenthesises = new Stack<char>();

            try
            {
                foreach (var c in value)
                    if (pairs.Keys.Contains(c))
                    {
                        parenthesises.Push(c);
                    }
                    else
                    {
                        if (pairs.Values.Contains(c))
                        {
                            if (c == pairs[parenthesises.First()])
                                parenthesises.Pop();
                            else
                                return false;
                        }
                        else
                        {
                        }
                    }
            }
            catch
            {
                return false;
            }

            return !parenthesises.Any() ? true : false;
        }
    }
}