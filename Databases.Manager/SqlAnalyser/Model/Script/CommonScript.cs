using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Script
{
    public class CommonScript : DbScript
    {
        public List<TokenInfo> Functions { get; set; } = new List<TokenInfo>();
        public List<Statement.Statement> Statements { get; set; } = new List<Statement.Statement>();
    }
}