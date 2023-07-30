using DatabaseInterpreter.Model;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class DropStatement : Statement, IStatementScriptBuilder
    {
        public DatabaseObjectType ObjectType { get; set; }
        public NameToken ObjectName { get; set; }
        public bool IsTemporaryTable { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}