namespace Databases.SqlAnalyser.Model.Statement
{
    public class BreakStatement : Statement, IStatementScriptBuilder
    {

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}