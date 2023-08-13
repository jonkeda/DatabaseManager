namespace Databases.SqlAnalyser.Model.Statement
{
    public class ContinueStatement : Statement, IStatementScriptBuilder
    {
        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}