using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class IfStatement : Statement, IStatementScriptBuilder
    {
        public List<IfStatementItem> Items { get; set; } = new List<IfStatementItem>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public class IfStatementItem
    {
        public IfStatementType Type { get; set; }
        public TokenInfo Condition { get; set; }
        public IfConditionType ConditionType { get; set; } = IfConditionType.Common;
        public SelectStatement CondtionStatement { get; set; }
        public List<Statement> Statements { get; set; } = new List<Statement>();
    }

    public enum IfStatementType
    {
        IF = 0,
        ELSEIF = 1,
        ELSE = 2
    }

    public enum IfConditionType
    {
        Common,
        Exists,
        NotExists
    }
}