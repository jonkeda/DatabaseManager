using Databases.Model.DatabaseObject.Fiction;

namespace Databases.Model.DatabaseObject
{
    public class TableConstraint : TableChild
    {
        public string ColumnName { get; set; }
        public string Definition { get; set; }
    }
}