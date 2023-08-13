using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class WithStatement : Statement
    {
        public TokenInfo CTE { get; set; }
        public TableName Name { get; set; }
        public List<ColumnName> Columns { get; set; } = new List<ColumnName>();
        public List<SelectStatement> SelectStatements { get; set; }
    }
}