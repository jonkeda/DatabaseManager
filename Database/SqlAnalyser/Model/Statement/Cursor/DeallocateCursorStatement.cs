using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class DeallocateCursorStatement : Statement
    {
        public TokenInfo CursorName { get; set; }
    }
}