using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class DeclareCursorStatement : Statement
    {
        public TokenInfo CursorName { get; set; }
        public SelectStatement SelectStatement { get; set; }
    }
}