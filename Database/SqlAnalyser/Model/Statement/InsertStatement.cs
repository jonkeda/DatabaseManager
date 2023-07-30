using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class InsertStatement : Statement, IStatementScriptBuilder
    {
        public TableName TableName { get; set; }
        public List<ColumnName> Columns { get; set; } = new List<ColumnName>();
        public List<TokenInfo> Values { get; set; } = new List<TokenInfo>();
        public List<SelectStatement> SelectStatements { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }
}