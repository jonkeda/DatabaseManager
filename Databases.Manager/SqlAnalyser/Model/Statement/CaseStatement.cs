using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class CaseStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo VariableName { get; set; }
        public List<IfStatementItem> Items { get; set; } = new List<IfStatementItem>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}