using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Databases.Model.DatabaseObject;
using Databases.SqlAnalyser.Model;
using Databases.SqlAnalyser.Model.DatabaseObject;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser
{
    public abstract class SqlRuleAnalyser
    {
        protected SqlRuleAnalyser(string content)
        {
            Content = content;
        }

        public string Content { get; set; }

        public SqlRuleAnalyserOption Option { get; set; } = new SqlRuleAnalyserOption();
        public abstract IEnumerable<Type> ParseTableTypes { get; }
        public abstract IEnumerable<Type> ParseColumnTypes { get; }
        public abstract IEnumerable<Type> ParseTableAliasTypes { get; }
        public abstract IEnumerable<Type> ParseColumnAliasTypes { get; }

        protected abstract Lexer GetLexer();

        protected virtual ICharStream GetCharStreamFromString()
        {
            if (string.IsNullOrEmpty(Content))
            {
                throw new Exception("Content can't be empty.");
            }

            return CharStreams.fromString(Content);
        }

        protected abstract Parser GetParser(CommonTokenStream tokenStream);

        protected virtual Parser GetParser()
        {
            var lexer = GetLexer();

            var tokens = new CommonTokenStream(lexer);

            var parser = GetParser(tokens);

            return parser;
        }

        protected SqlSyntaxErrorListener AddParserErrorListener(Parser parser)
        {
            var errorListener = new SqlSyntaxErrorListener();

            parser.AddErrorListener(errorListener);

            return errorListener;
        }

        protected abstract TableName ParseTableName(ParserRuleContext node, bool strict = false);
        protected abstract ColumnName ParseColumnName(ParserRuleContext node, bool strict = false);
        protected abstract TokenInfo ParseTableAlias(ParserRuleContext node);
        protected abstract TokenInfo ParseColumnAlias(ParserRuleContext node);
        protected abstract bool IsFunction(IParseTree node);

        protected virtual TokenInfo ParseFunction(ParserRuleContext node)
        {
            var token = new TokenInfo(node);
            return token;
        }

        public virtual AnalyseResult Analyse<T>(string content)
            where T : DatabaseObject
        {
            AnalyseResult result = null;

            if (typeof(T) == typeof(Procedure))
            {
                result = AnalyseProcedure();
            }
            else if (typeof(T) == typeof(Function))
            {
                result = AnalyseFunction();
            }
            else if (typeof(T) == typeof(View))
            {
                result = AnalyseView();
            }
            else if (typeof(T) == typeof(TableTrigger))
            {
                result = AnalyseTrigger();
            }
            else
            {
                throw new NotSupportedException($"Not support analyse for type:{typeof(T).Name}");
            }

            return result;
        }

        public abstract SqlSyntaxError Validate();

        public abstract AnalyseResult AnalyseCommon();

        public abstract AnalyseResult AnalyseProcedure();

        public abstract AnalyseResult AnalyseFunction();

        public abstract AnalyseResult AnalyseTrigger();

        public abstract AnalyseResult AnalyseView();

        public virtual void ExtractFunctions(CommonScript script, ParserRuleContext node)
        {
            if (!Option.ExtractFunctions)
            {
                return;
            }

            if (node == null || node.children == null)
            {
                return;
            }

            foreach (var child in node.children)
            {
                var isFunction = false;

                if (IsFunction(child))
                {
                    isFunction = true;

                    var childNode = child as ParserRuleContext;

                    if (!script.Functions.Any(item =>
                            item.StartIndex.Value == childNode.Start.StartIndex &&
                            item.StopIndex.Value == childNode.Stop.StopIndex))
                    {
                        script.Functions.Add(ParseFunction(childNode));
                    }
                }

                if (isFunction && !Option.ExtractFunctionChildren)
                {
                    continue;
                }

                if (child is ParserRuleContext context)
                {
                    ExtractFunctions(script, context);
                }
            }
        }

        protected TokenInfo CreateToken(ParserRuleContext node, TokenType tokenType = TokenType.General)
        {
            var tokenInfo = new TokenInfo(node) { Type = tokenType };

            return tokenInfo;
        }

        protected bool HasAsFlag(ParserRuleContext node)
        {
            return node.children.Count > 0 && node.GetChild(0).GetText() == "AS";
        }

        protected List<ParserRuleContext> FindSpecificContexts(ParserRuleContext node, IEnumerable<Type> searchTypes)
        {
            var fullNames = new List<ParserRuleContext>();

            if (node != null && node.children != null)
            {
                foreach (var child in node.children)
                {
                    if (searchTypes.Any(t => t == child.GetType()))
                    {
                        var c = child as ParserRuleContext;

                        fullNames.Add(c);
                    }
                    else if (!(child is TerminalNodeImpl))
                    {
                        fullNames.AddRange(FindSpecificContexts(child as ParserRuleContext, searchTypes));
                    }
                }
            }

            return fullNames;
        }

        protected List<TokenInfo> ParseTableAndColumnNames(ParserRuleContext node, bool isOnlyForColumn = false)
        {
            var tokens = new List<TokenInfo>();
            var tableNameTokens = new List<TableName>();
            var columnNameTokens = new List<ColumnName>();
            var aliasTokens = new List<TokenInfo>();

            var types = isOnlyForColumn
                ? ParseColumnTypes.Union(ParseColumnAliasTypes)
                : ParseTableTypes.Union(ParseColumnTypes).Union(ParseTableAliasTypes).Union(ParseColumnAliasTypes);

            var results = FindSpecificContexts(node, types);

            var tableNames = !isOnlyForColumn
                ? results.Where(item => ParseTableTypes.Any(t => item.GetType() == t))
                : Enumerable.Empty<ParserRuleContext>();
            var columnNames = results.Where(item => ParseColumnTypes.Any(t => item.GetType() == t));
            var tableAliases = !isOnlyForColumn
                ? results.Where(item => ParseTableAliasTypes.Any(t => item.GetType() == t))
                : Enumerable.Empty<ParserRuleContext>();
            var columnAliases = results.Where(item => ParseColumnAliasTypes.Any(t => item.GetType() == t));

            foreach (var columnName in columnNames)
            {
                columnNameTokens.Add(ParseColumnName(columnName));
            }

            if (!isOnlyForColumn)
            {
                foreach (var tableName in tableNames)
                {
                    tableNameTokens.Add(ParseTableName(tableName));
                }
            }

            foreach (var columnAlias in columnAliases)
            {
                var alias = ParseColumnAlias(columnAlias);

                if (!IsAliasExisted(columnNameTokens, alias))
                {
                    aliasTokens.Add(alias);
                }
            }

            if (!isOnlyForColumn)
            {
                foreach (var tableAlias in tableAliases)
                {
                    var alias = ParseTableAlias(tableAlias);

                    if (!IsAliasExisted(tableNameTokens, alias))
                    {
                        aliasTokens.Add(alias);
                    }
                }
            }

            tokens.AddRange(columnNameTokens);

            if (!isOnlyForColumn)
            {
                tokens.AddRange(tableNameTokens);
            }

            tokens.AddRange(aliasTokens);

            return tokens;
        }

        private bool IsAliasExisted(IEnumerable<NameToken> tokens, TokenInfo alias)
        {
            if (tokens.Any(item =>
                    item.Alias?.Symbol == alias?.Symbol && item.Alias?.StartIndex == alias?.StartIndex))
            {
                return true;
            }

            return false;
        }

        protected void AddChildTableAndColumnNameToken(ParserRuleContext node, TokenInfo token)
        {
            if (Option.ParseTokenChildren)
            {
                if (token != null)
                {
                    ParseTableAndColumnNames(node).ForEach(item => token.AddChild(item));
                }
            }
        }

        protected void AddChildColumnNameToken(ParserRuleContext node, TokenInfo token, IEnumerable<Type> searchTypes)
        {
            if (Option.ParseTokenChildren)
            {
                if (token != null)
                {
                    ParseTableAndColumnNames(node, true).ForEach(item => token.AddChild(item));
                }
            }
        }

        protected void AddChildColumnNameToken(ParserRuleContext node, TokenInfo token)
        {
            if (Option.ParseTokenChildren)
            {
                AddChildColumnNameToken(node, token, ParseColumnTypes);
            }
        }

        protected ConstraintType GetConstraintType(TerminalNodeImpl node)
        {
            var constraintType = ConstraintType.None;

            var text = node.GetText().ToUpper();

            switch (text)
            {
                case "PRIMARY":
                    constraintType = ConstraintType.PrimaryKey;
                    break;
                case "FOREIGN":
                case "REFERENCES":
                    constraintType = ConstraintType.ForeignKey;
                    break;
                case "UNIQUE":
                    constraintType = ConstraintType.UniqueIndex;
                    break;
                case "CHECK":
                    constraintType = ConstraintType.Check;
                    break;
                case "DEFAULT":
                    constraintType = ConstraintType.Default;
                    break;
            }

            return constraintType;
        }
    }
}