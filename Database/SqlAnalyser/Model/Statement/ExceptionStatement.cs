using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class ExceptionStatement : Statement
    {
        public List<ExceptionItem> Items { get; set; } = new List<ExceptionItem>();
    }

    public class ExceptionItem
    {
        public TokenInfo Name { get; set; }
        public List<Statement> Statements { get; set; } = new List<Statement>();
    }
}