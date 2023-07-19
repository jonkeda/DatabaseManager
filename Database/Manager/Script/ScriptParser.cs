using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Core;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class ScriptParser
    {
        public const string AsPattern = @"\b(AS|IS)\b";
        private readonly string createAlterScriptPattern = @"\b(CREATE|ALTER).+(VIEW|FUNCTION|PROCEDURE|TRIGGER)\b";
        private readonly string dmlPattern = @"\b(CREATE|ALTER|INSERT|UPDATE|DELETE|TRUNCATE|INTO)\b";
        private readonly string routinePattern = @"\b(BEGIN|END|DECLARE|SET|GOTO)\b";
        private readonly string selectPattern = "SELECT(.[\n]?)+(FROM)?";
        private readonly DbInterpreter dbInterpreter;

        public ScriptParser(DbInterpreter dbInterpreter, string script)
        {
            this.dbInterpreter = dbInterpreter;
            Script = script;

            Parse();
        }

        public string Script { get; }

        public string CleanScript { get; private set; }

        private string Parse()
        {
            var sb = new StringBuilder();

            var lines = Script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith(dbInterpreter.CommentString)) continue;

                sb.AppendLine(line);
            }

            CleanScript = sb.ToString();

            return CleanScript;
        }

        public bool IsSelect()
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.Multiline;

            var selectMatches = Regex.Matches(Script, selectPattern, options);

            if (selectMatches.Cast<Match>().Any(item => !IsWordInSingleQuotation(CleanScript, item.Index)))
            {
                var dmlMatches = Regex.Matches(Script, dmlPattern, options);
                var routineMathes = Regex.Matches(Script, routinePattern, RegexOptions.IgnoreCase);

                if (!(dmlMatches.Cast<Match>().Any(item => !IsWordInSingleQuotation(Script, item.Index))
                      || routineMathes.Cast<Match>().Any(item => !IsWordInSingleQuotation(Script, item.Index))))
                    return true;
            }

            return false;
        }

        private bool IsWordInSingleQuotation(string content, int startIndex)
        {
            return content.Substring(0, startIndex).Count(item => item == '\'') % 2 != 0;
        }

        public bool IsCreateOrAlterScript()
        {
            var mathes = Regex.Matches(Script, createAlterScriptPattern,
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return mathes.Cast<Match>().Any(item => !IsWordInSingleQuotation(Script, item.Index));
        }

        public static ScriptType DetectScriptType(string script, DbInterpreter dbInterpreter)
        {
            var upperScript = script.ToUpper().Trim();

            var scriptParser = new ScriptParser(dbInterpreter, upperScript);

            if (scriptParser.IsCreateOrAlterScript())
            {
                var firstLine = upperScript.Split(new [] { Environment.NewLine }, StringSplitOptions.None).FirstOrDefault();

                var asMatch = Regex.Match(firstLine, AsPattern);

                var asIndex = asMatch.Index;

                if (asIndex <= 0) asIndex = firstLine.Length;

                var prefix = upperScript.Substring(0, asIndex);

                if (prefix.IndexOf(" VIEW ", StringComparison.Ordinal) > 0)
                    return ScriptType.View;
                if (prefix.IndexOf(" FUNCTION ", StringComparison.Ordinal) > 0)
                    return ScriptType.Function;
                if (prefix.IndexOf(" PROCEDURE ", StringComparison.Ordinal) > 0 || prefix.IndexOf(" PROC ", StringComparison.Ordinal) > 0)
                    return ScriptType.Procedure;
                if (prefix.IndexOf(" TRIGGER ", StringComparison.Ordinal) > 0) return ScriptType.Trigger;
            }
            else if (scriptParser.IsSelect())
            {
                return ScriptType.SimpleSelect;
            }

            return ScriptType.Other;
        }

        public static ScriptContentInfo GetContentInfo(string script, string lineSeperator, string commentString)
        {
            var lineSeperatorLength = lineSeperator.Length;

            var info = new ScriptContentInfo();

            var lines = script.Split(new []{ lineSeperator}, StringSplitOptions.None);

            var count = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var lineInfo = new TextLineInfo { Index = i, FirstCharIndex = count };

                if (lines[i].Trim().StartsWith(commentString) || lines[i].Trim().StartsWith("**"))
                    lineInfo.Type = TextLineType.Comment;

                if (i < lines.Length - 1)
                    lineInfo.Length = lines[i].Length + lineSeperatorLength;
                else
                    lineInfo.Length = script.EndsWith(lineSeperator)
                        ? lines[i].Length + lineSeperatorLength
                        : lines[i].Length;

                count += lines[i].Length + lineSeperatorLength;

                info.Lines.Add(lineInfo);
            }

            return info;
        }

        public static string ExtractScriptBody(string definition)
        {
            var match = MatchWord(definition, "BEGIN|AS");

            if (match != null) return definition.Substring(match.Index + match.Value.Length);

            return definition;
        }

        public static int GetBeginAsIndex(string definition)
        {
            var match = MatchWord(definition, "AS");

            if (match == null)
            {
                match = MatchWord(definition, "BEGIN");

                if (match != null) return match.Index;
            }
            else
            {
                return match.Index;
            }

            return -1;
        }

        private static Match MatchWord(string value, string word)
        {
            return Regex.Match(value, $@"\b({word})\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }

    public enum ScriptType
    {
        SimpleSelect = 1,
        View = 2,
        Function = 3,
        Procedure = 4,
        Trigger = 5,
        Other = 6
    }
}