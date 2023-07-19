using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using TSQL;
using TSQL.Tokens;

namespace DatabaseConverter.Core
{
    public class DbObjectTokenTranslator : DbObjectTranslator
    {
        private readonly List<string> convertedDataTypes = new List<string>();
        private readonly List<string> convertedFunctions = new List<string>();

        private List<FunctionSpecification> sourceFuncSpecs;
        private List<FunctionSpecification> targetFuncSpecs;

        public DbObjectTokenTranslator(DbInterpreter source, DbInterpreter target) : base(source, target)
        {
        }

        public override void Translate()
        {
        }

        public virtual string ParseDefinition(string definition)
        {
            var tokens = GetTokens(definition);

            definition = HandleDefinition(definition, tokens, out var changed);

            if (changed) tokens = GetTokens(definition);

            definition = BuildDefinition(tokens);

            return definition;
        }

        protected string HandleDefinition(string definition, List<TSQLToken> tokens, out bool changed)
        {
            sourceFuncSpecs = FunctionManager.GetFunctionSpecifications(sourceDbInterpreter.DatabaseType);
            targetFuncSpecs = FunctionManager.GetFunctionSpecifications(targetDbInterpreter.DatabaseType);

            changed = false;

            var newDefinition = definition;

            foreach (var token in tokens)
            {
                var text = token.Text;
                string functionExpression = null;

                switch (token.Type)
                {
                    case TSQLTokenType.SystemIdentifier:
                    case TSQLTokenType.Identifier:

                        if (sourceFuncSpecs.Any(item => item.Name == text.ToUpper()))
                            functionExpression = GetFunctionExpression(token, definition);

                        break;
                    case TSQLTokenType.Keyword:
                        break;
                }

                if (!string.IsNullOrEmpty(functionExpression))
                {
                    var useBrackets = false;
                    var targetFunctionInfo = GetMappingFunctionInfo(text, null, out useBrackets);

                    var formula = new FunctionFormula(functionExpression);

                    Dictionary<string, string> dictDataType = null;

                    if (formula.Name == null) continue;

                    var newExpression = ParseFormula(sourceFuncSpecs, targetFuncSpecs, formula, targetFunctionInfo,
                        out dictDataType);

                    if (newExpression != formula.Expression)
                    {
                        newDefinition = ReplaceValue(newDefinition, formula.Expression, newExpression);

                        changed = true;
                    }

                    if (dictDataType != null) convertedDataTypes.AddRange(dictDataType.Values);

                    if (!string.IsNullOrEmpty(targetFunctionInfo.Args) && changed)
                        if (!convertedFunctions.Contains(targetFunctionInfo.Name))
                            convertedFunctions.Add(targetFunctionInfo.Name);
                }
            }

            return newDefinition;
        }

        private string GetFunctionExpression(TSQLToken token, string definition)
        {
            int startIndex = token.BeginPosition;
            int functionEndIndex = FindFunctionEndIndex(startIndex + token.Text.Length, definition);

            string functionExpression = null;

            if (functionEndIndex != -1)
                functionExpression = definition.Substring(startIndex, functionEndIndex - startIndex + 1);

            return functionExpression;
        }

        private int FindFunctionEndIndex(int startIndex, string definition)
        {
            var leftBracketCount = 0;
            var rightBracketCount = 0;
            var functionEndIndex = -1;

            for (var i = startIndex; i < definition.Length; i++)
            {
                if (definition[i] == '(')
                    leftBracketCount++;
                else if (definition[i] == ')') rightBracketCount++;

                if (rightBracketCount == leftBracketCount)
                {
                    functionEndIndex = i;
                    break;
                }
            }

            return functionEndIndex;
        }

        public string BuildDefinition(List<TSQLToken> tokens)
        {
            var sb = new StringBuilder();

            sourceSchemaName = sourceDbInterpreter.DefaultSchema;

            var sourceDataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(sourceDbInterpreter.DatabaseType);
            var targetDataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(targetDbInterpreter.DatabaseType);

            var ignoreCount = 0;

            var previousType = TSQLTokenType.Whitespace;
            var previousText = "";

            for (var i = 0; i < tokens.Count; i++)
            {
                if (ignoreCount > 0)
                {
                    ignoreCount--;
                    continue;
                }

                var token = tokens[i];

                var tokenType = token.Type;
                var text = token.Text;

                switch (tokenType)
                {
                    case TSQLTokenType.Identifier:

                        var nextToken = i + 1 < tokens.Count ? tokens[i + 1] : null;

                        if (convertedDataTypes.Contains(text))
                        {
                            sb.Append(text);
                            continue;
                        }

                        //Remove schema name
                        if (nextToken != null && nextToken.Text.Trim() != "(" &&
                            text.Trim('"') == sourceSchemaName && i + 1 < tokens.Count && tokens[i + 1].Text == "."
                           )
                        {
                            ignoreCount++;
                            continue;
                        }

                        if (nextToken != null && nextToken.Text.Trim() == "(") //function handle
                        {
                            if (convertedFunctions.Contains(text))
                            {
                                sb.Append(text);
                                continue;
                            }

                            var targetFunctionInfo = GetMappingFunctionInfo(text, null, out var useBrackets);

                            if (targetFunctionInfo.Name.ToLower() != text.ToLower())
                            {
                                var targetFunction = targetFunctionInfo.Name;

                                if (!string.IsNullOrEmpty(targetFunction))
                                    sb.Append(targetFunction);
                                else
                                    sb.Append(text); //reserve original function name

                                if (useBrackets) ignoreCount += 2;
                            }
                            else
                            {
                                if (text.StartsWith(sourceDbInterpreter.QuotationLeftChar.ToString()) &&
                                    text.EndsWith(sourceDbInterpreter.QuotationRightChar.ToString()))
                                    sb.Append(GetQuotedString(text.Trim(sourceDbInterpreter.QuotationLeftChar,
                                        sourceDbInterpreter.QuotationRightChar)));
                                else
                                    sb.Append(text);
                            }
                        }
                        else
                        {
                            if ((sourceDataTypeSpecs != null && sourceDataTypeSpecs.Any(item => item.Name == text))
                                || (targetDataTypeSpecs != null && targetDataTypeSpecs.Any(item => item.Name == text)))
                                sb.Append(text);
                            else
                                sb.Append(GetQuotedString(text));
                        }

                        break;
                    case TSQLTokenType.StringLiteral:
                        if (previousType != TSQLTokenType.Whitespace && previousText.ToLower() == "as")
                            sb.Append(GetQuotedString(text));
                        else
                            sb.Append(text);
                        break;
                    case TSQLTokenType.SingleLineComment:
                    case TSQLTokenType.MultilineComment:
                        continue;
                    case TSQLTokenType.Keyword:
                        switch (text.ToUpper())
                        {
                            case "AS":
                                if (targetDbInterpreter is OracleInterpreter)
                                {
                                    var previousKeyword =
                                        (from t in tokens
                                            where t.Type == TSQLTokenType.Keyword && t.EndPosition < token.BeginPosition
                                            select t).LastOrDefault();
                                    if (previousKeyword != null && previousKeyword.Text.ToUpper() == "FROM") continue;
                                }

                                break;
                        }

                        sb.Append(text);
                        break;
                    default:
                        sb.Append(text);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    previousText = text;
                    previousType = tokenType;
                }
            }

            return sb.ToString();
        }

        private string GetQuotedString(string text)
        {
            if (!text.StartsWith(targetDbInterpreter.QuotationLeftChar.ToString()) &&
                !text.EndsWith(targetDbInterpreter.QuotationRightChar.ToString()))
                return targetDbInterpreter.GetQuotedString(text.Trim('\'', '"', sourceDbInterpreter.QuotationLeftChar,
                    sourceDbInterpreter.QuotationRightChar));

            return text;
        }

        public List<TSQLToken> GetTokens(string sql)
        {
            return TSQLTokenizer.ParseTokens(sql, true, true);
        }
    }
}