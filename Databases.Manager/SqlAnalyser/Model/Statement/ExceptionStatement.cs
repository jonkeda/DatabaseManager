using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class ExceptionStatement : Statement, IStatementScriptBuilder
    {
        public List<ExceptionItem> Items { get; set; } = new List<ExceptionItem>();

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public class ExceptionItem
    {
        public TokenInfo Name { get; set; }
        public List<Statement> Statements { get; set; } = new List<Statement>();
    }
}