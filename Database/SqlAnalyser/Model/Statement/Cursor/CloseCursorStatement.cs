using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class CloseCursorStatement : Statement
    {
        public TokenInfo CursorName { get; set; }
        public bool IsEnd { get; set; }
    }
}