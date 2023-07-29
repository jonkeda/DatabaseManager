using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class ReturnStatement : Statement
    {
        public TokenInfo Value { get; set; }
    }
}