using System.Collections.Generic;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class TryCatchStatement : Statement
    {
        public List<Statement> TryStatements { get; set; } = new List<Statement>();
        public List<Statement> CatchStatements { get; set; } = new List<Statement>();
    }
}