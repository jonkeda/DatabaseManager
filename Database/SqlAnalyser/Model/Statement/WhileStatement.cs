using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class WhileStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo Condition { get; set; }

        public List<Statement> Statements { get; set; } = new List<Statement>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}