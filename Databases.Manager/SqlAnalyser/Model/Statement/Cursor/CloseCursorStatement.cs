using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class CloseCursorStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo CursorName { get; set; }
        public bool IsEnd { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}