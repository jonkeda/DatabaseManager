using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class DeleteStatement : Statement, IStatementScriptBuilder
    {
        public TableName TableName { get; set; }
        public List<FromItem> FromItems { get; set; }
        public TokenInfo Condition { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}