using System;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Utility;

namespace DatabaseManager.Helper
{
    public class FrontQueryHelper
    {
        public static bool NeedQuotedForSql(Type type)
        {
            var typeName = type.Name;

            if (type == typeof(char) ||
                type == typeof(string) ||
                type == typeof(Guid) ||
                typeName == "SqlHierarchyId" ||
                DataTypeHelper.IsDateOrTimeType(typeName) ||
                DataTypeHelper.IsGeometryType(typeName)
               )
                return true;

            return false;
        }

        public static string GetSafeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            value = Regex.Replace(value, @";", string.Empty);
            value = Regex.Replace(value, @"'", string.Empty);
            value = Regex.Replace(value, @"&", string.Empty);
            value = Regex.Replace(value, @"%20", string.Empty);
            value = Regex.Replace(value, @"--", string.Empty);
            value = Regex.Replace(value, @"==", string.Empty);
            value = Regex.Replace(value, @"<", string.Empty);
            value = Regex.Replace(value, @">", string.Empty);
            value = Regex.Replace(value, @"%", string.Empty);

            return value;
        }
    }
}