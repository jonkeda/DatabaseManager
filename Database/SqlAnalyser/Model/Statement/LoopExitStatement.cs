using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class LoopExitStatement : Statement
    {
        public TokenInfo Condition { get; set; }
        public bool IsCursorLoopExit { get; set; }
    }
}