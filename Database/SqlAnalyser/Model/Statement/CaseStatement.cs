using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class CaseStatement : Statement
    {
        public TokenInfo VariableName { get; set; }
        public List<IfStatementItem> Items { get; set; } = new List<IfStatementItem>();
    }
}