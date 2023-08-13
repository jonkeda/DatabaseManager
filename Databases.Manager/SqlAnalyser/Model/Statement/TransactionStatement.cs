using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class TransactionStatement : Statement, IStatementScriptBuilder
    {
        public TransactionCommandType CommandType { get; set; }
        public TokenInfo Content { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public enum TransactionCommandType
    {
        BEGIN = 1,
        COMMIT = 2,
        ROLLBACK = 3,
        SET = 4
    }
}