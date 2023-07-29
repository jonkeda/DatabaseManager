using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class LeaveStatement : Statement
    {
        public TokenInfo Content { get; set; }
    }
}