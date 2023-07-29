using Databases.SqlAnalyser.Model.DatabaseObject;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class DeclareTableStatement : Statement
    {
        public TableInfo TableInfo { get; set; }
    }
}