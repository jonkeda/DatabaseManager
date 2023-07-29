using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace Databases.Interpreter.Builder
{
    public class ScriptBuilder
    {
        private static readonly Regex FormatRegex = new Regex(@"([;]+[\s]*[;]+)|(\r\n[\s]*[;])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public bool FormatScript { get; set; } = true;
        public List<Script> Scripts { get; } = new List<Script>();

        public void Append(Script script)
        {
            Scripts.Add(script);
        }

        public void AppendLine(Script script)
        {
            Append(script);
            AppendLine();
        }

        public void AppendLine()
        {
            Append(new NewLineSript());
        }

        public void AppendRange(IEnumerable<Script> scripts)
        {
            Scripts.AddRange(scripts);
        }

        public override string ToString()
        {
            var script = string.Join("", Scripts.Select(item => item.Content)).Trim();

            return FormatScript ? Format(script) : script;
        }

        private string Format(string script)
        {
            return StringHelper.ToSingleEmptyLine(FormatRegex.Replace(script, ";"));
        }
    }
}