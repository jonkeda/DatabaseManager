using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class GotoStatement : Statement, IStatementScriptBuilder
    {
        public bool IsLabel => Statements.Count == 0;
        public TokenInfo Label { get; set; }

        public List<Statement> Statements { get; set; } = new List<Statement>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}