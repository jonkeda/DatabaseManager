using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class SetStatement : Statement, IStatementScriptBuilder
    {
        public TokenInfo Key { get; set; }
        public TokenInfo Value { get; set; }

        public bool IsSetUserVariable => Key?.Type == TokenType.UserVariableName;
        public bool IsSetCursorVariable { get; set; }
        public SelectStatement ValueStatement { get; set; }

        public UserVariableDataType UserVariableDataType { get; set; } = UserVariableDataType.Unknown;

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public enum UserVariableDataType
    {
        Unknown,
        String,
        Integer,
        Decimal
    }
}