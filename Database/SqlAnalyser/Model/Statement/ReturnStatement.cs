using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class ReturnStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo Value { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}