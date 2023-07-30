using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class LoopStatement : WhileStatement, IStatementScriptBuilder
    {
        public LoopType Type { get; set; }
        public TokenInfo Name { get; set; }
        public LoopCursorInfo LoopCursorInfo { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public class LoopCursorInfo
    {
        public TokenInfo IteratorName { get; set; }
        public TokenInfo StartValue { get; set; }
        public TokenInfo StopValue { get; set; }
        public bool IsReverse { get; set; }
        public bool IsIntegerIterate { get; set; }
        public SelectStatement SelectStatement { get; set; }
    }

    public enum LoopType
    {
        LOOP = 0,
        WHILE = 1,
        FOR = 2
    }
}