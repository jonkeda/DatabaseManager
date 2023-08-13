using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class LoopExitStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo Condition { get; set; }
        public bool IsCursorLoopExit { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}