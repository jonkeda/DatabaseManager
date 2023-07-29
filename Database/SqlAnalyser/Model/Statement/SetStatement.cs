using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class SetStatement : Statement
    {
        public TokenInfo Key { get; set; }
        public TokenInfo Value { get; set; }

        public bool IsSetUserVariable => Key?.Type == TokenType.UserVariableName;
        public bool IsSetCursorVariable { get; set; }
        public SelectStatement ValueStatement { get; set; }

        public UserVariableDataType UserVariableDataType { get; set; } = UserVariableDataType.Unknown;
    }

    public enum UserVariableDataType
    {
        Unknown,
        String,
        Integer,
        Decimal
    }
}