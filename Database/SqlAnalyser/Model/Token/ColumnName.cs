using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace SqlAnalyser.Model
{
    public class ColumnName : NameToken
    {
        private TokenInfo tableName;

        //public string FullName
        //{
        //    get
        //    {
        //        if (this.tableName == null)
        //        {
        //            return this.Symbol?.ToString();
        //        }
        //        else
        //        {
        //            return $"{this.tableName}.{this.Symbol}";
        //        }
        //    }
        //}

        public ColumnName(string symbol) : base(symbol)
        { }

        public ColumnName(ParserRuleContext context) : base(context)
        { }

        public ColumnName(string symbol, ParserRuleContext context) : base(symbol, context)
        { }

        public ColumnName(ITerminalNode node) : base(node)
        { }

        public ColumnName(string symbol, ITerminalNode node) : base(symbol, node)
        { }

        public override TokenType Type => TokenType.ColumnName;

        public TokenInfo DataType { get; set; }

        public TokenInfo TableName
        {
            get => tableName;
            set
            {
                value.Type = TokenType.TableName;
                tableName = value;
            }
        }

        public string FieldName
        {
            get
            {
                if (alias != null)
                {
                    return alias.Symbol;
                }

                return Symbol;
            }
        }
    }
}