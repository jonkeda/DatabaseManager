using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class DeclareCursorHandlerStatement : Statement, IStatementScriptBuilder
    {
        public List<Statement> Statements = new List<Statement>();
        public List<TokenInfo> Conditions { get; set; } = new List<TokenInfo>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}