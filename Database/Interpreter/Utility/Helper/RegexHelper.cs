using System.Text.RegularExpressions;

namespace DatabaseInterpreter.Utility
{
    public class RegexHelper
    {
        public static Regex NameRegex { get; } = new Regex(NameRegexPattern, RegexOptions.Compiled);
        public static Regex NumberRegex { get; } = new Regex(NumberRegexPattern, RegexOptions.Compiled);

        private const string NameRegexPattern = "^[a-zA-Z_][a-zA-Z0-9_]*$";
        private const string NumberRegexPattern = "(([0-9]\\d*\\.?\\d*)|(0\\.\\d*[0-9]))";
        //public const string ParenthesesRegexPattern = @"\((.|\n|\r)*\)";
        //public const string EscapeChars = @".()[^$+*?|\{";

        public static string Replace(string input, string pattern, string replacement, RegexOptions options)
        {
            var escapedPattern = Regex.Escape(pattern);
            var escapedReplacement = CheckReplacement(replacement);

            return Regex.Replace(input, escapedPattern, escapedReplacement, options);
        }

        public static string CheckReplacement(string replacement)
        {
            //https://learn.microsoft.com/en-us/dotnet/standard/base-types/substitutions-in-regular-expressions
            if (replacement != null && replacement.Contains("$")) return replacement.Replace("$", "$$");

            return replacement;
        }
    }
}