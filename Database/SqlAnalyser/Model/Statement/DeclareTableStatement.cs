using Databases.SqlAnalyser.Model.DatabaseObject;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class DeclareTableStatement : Statement, IStatementScriptBuilder
    {
        public TableInfo TableInfo { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}