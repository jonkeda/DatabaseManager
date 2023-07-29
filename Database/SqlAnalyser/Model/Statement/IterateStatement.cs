using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class IterateStatement : Statement
    {
        public TokenInfo Content { get; set; }
    }
}