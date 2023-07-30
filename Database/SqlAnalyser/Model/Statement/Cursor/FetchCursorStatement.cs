using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class FetchCursorStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo CursorName { get; set; }
        public List<TokenInfo> Variables { get; set; } = new List<TokenInfo>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}