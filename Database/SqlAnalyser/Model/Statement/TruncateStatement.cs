using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class TruncateStatement : Statement
    {
        public TableName TableName { get; set; }
    }
}