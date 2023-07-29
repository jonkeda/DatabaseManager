using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement.Cursor
{
    public class OpenCursorStatement : Statement
    {
        public TokenInfo CursorName { get; set; }
    }
}