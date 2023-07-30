using DatabaseInterpreter.Model;
using Databases.SqlAnalyser.Model.DatabaseObject;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class CreateTableStatement : CreateStatement, IStatementScriptBuilder
    {
        public override DatabaseObjectType ObjectType => DatabaseObjectType.Table;
        public TableInfo TableInfo { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}