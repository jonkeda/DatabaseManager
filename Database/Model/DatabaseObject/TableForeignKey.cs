using System.Collections.Generic;

namespace DatabaseInterpreter.Model
{
    public class TableForeignKey : TableChild
    {
        public string ReferencedSchema { get; set; }
        public string ReferencedTableName { get; set; }
        public bool UpdateCascade { get; set; }
        public bool DeleteCascade { get; set; }

        public List<ForeignKeyColumn> Columns { get; set; } = new List<ForeignKeyColumn>();
    }

    public class ForeignKeyColumn : SimpleColumn
    {
        public string ReferencedColumnName { get; set; }
    }

    public class TableForeignKeyItem : TableColumnChild
    {
        public string ReferencedSchema { get; set; }
        public string ReferencedTableName { get; set; }
        public string ReferencedColumnName { get; set; }
        public bool UpdateCascade { get; set; }
        public bool DeleteCascade { get; set; }

        public string TableFullName => Schema + "." + TableName;
        public string ReferencedTableFullName => Schema + "." + ReferencedTableName;
    }
}