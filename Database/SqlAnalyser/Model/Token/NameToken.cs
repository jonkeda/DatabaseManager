using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace SqlAnalyser.Model
{
    public class NameToken : TokenInfo
    {
        protected TokenInfo alias;

        public NameToken(string symbol) : base(symbol)
        { }

        public NameToken(ParserRuleContext context) : base(context)
        { }

        public NameToken(string symbol, ParserRuleContext context) : base(symbol, context)
        { }

        public NameToken(ITerminalNode node) : base(node)
        { }

        public NameToken(string symbol, ITerminalNode node) : base(symbol, node)
        { }

        public string Server { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }

        public bool HasAs { get; set; }

        public TokenInfo Alias
        {
            get => alias;
            set
            {
                if (value != null)
                {
                    var type = GetType();

                    if (type == typeof(TableName))
                    {
                        value.Type = TokenType.TableAlias;
                    }
                    else if (type == typeof(ColumnName))
                    {
                        value.Type = TokenType.ColumnAlias;
                    }
                }

                alias = value;
            }
        }

        public string NameWithSchema
        {
            get
            {
                if (!string.IsNullOrEmpty(Schema))
                {
                    return $"{Schema}.{Symbol}";
                }

                return Symbol;
            }
        }

        public string NameWithAlias
        {
            get
            {
                if (alias == null)
                {
                    return Symbol;
                }

                var strAs = HasAs ? " AS " : " ";

                return $"{Symbol}{strAs}{alias}";
            }
        }
    }
}