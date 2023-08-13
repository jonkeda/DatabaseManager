using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class PreparedStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo Id { get; set; }
        public PreparedStatementType Type { get; set; }
        public TokenInfo FromSqlOrVariable { get; set; }
        public List<TokenInfo> ExecuteVariables { get; set; } = new List<TokenInfo>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public enum PreparedStatementType
    {
        Prepare,
        Execute,
        Deallocate
    }
}