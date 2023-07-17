using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace SqlAnalyser.Model
{
    public class TokenInfo
    {
        public TokenInfo(string symbol)
        {
            Symbol = symbol;
        }

        public TokenInfo(ParserRuleContext context)
        {
            Symbol = context?.GetText();
            SetIndex(context);
        }

        public TokenInfo(string symbol, ParserRuleContext context)
        {
            Symbol = symbol;
            SetIndex(context);
        }

        public TokenInfo(ITerminalNode node)
        {
            Symbol = node?.GetText();
            SetIndex(node);
        }

        public TokenInfo(string symbol, ITerminalNode node)
        {
            Symbol = symbol;
            SetIndex(node);
        }

        public virtual TokenType Type { get; set; }
        public string Symbol { get; set; }
        public int? StartIndex { get; set; }
        public int? StopIndex { get; set; }
        public bool IsConst { get; set; }

        public int Length => StartIndex.HasValue && StopIndex.HasValue ? (StopIndex - StartIndex + 1).Value : 0;

        public TokenInfo Parent { get; private set; }
        public List<TokenInfo> Children { get; } = new List<TokenInfo>();

        public TokenInfo SetIndex(ParserRuleContext context)
        {
            StartIndex = context?.Start?.StartIndex;
            StopIndex = context?.Stop?.StopIndex;

            return this;
        }

        public TokenInfo SetIndex(ITerminalNode node)
        {
            StartIndex = node?.Symbol?.StartIndex;
            StopIndex = node?.Symbol?.StopIndex;

            return this;
        }

        public override string ToString()
        {
            return Symbol;
        }

        public void AddChild(TokenInfo child)
        {
            if (child == null) return;

            if (!(child.StartIndex == StartIndex && child.StopIndex == StopIndex))
            {
                child.Parent = this;
                Children.Add(child);
            }
        }
    }

    public enum TokenType
    {
        General = 0,
        TableName,
        ViewName,
        TypeName,
        SequenceName,
        TriggerName,
        FunctionName,
        ProcedureName,
        RoutineName,
        ColumnName,
        ParameterName,
        VariableName,
        UserVariableName, //for mysql
        CursorName,
        ConstraintName,
        DataType,
        TableAlias,
        ColumnAlias,
        IfCondition, //not include query
        SearchCondition,
        TriggerCondition,
        ExitCondition,
        OrderBy,
        GroupBy,
        Option,
        JoinOn,
        Pivot,
        UnPivot,
        InsertValue,
        UpdateSetValue,
        Subquery,
        FunctionCall,
        StringLiteral
    }
}