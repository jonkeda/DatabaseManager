using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class DeclareCursorStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo CursorName { get; set; }
        public SelectStatement SelectStatement { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}