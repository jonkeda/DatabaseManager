using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class PrintStatement : Statement
    {
        public TokenInfo Content { get; set; }
    }
}