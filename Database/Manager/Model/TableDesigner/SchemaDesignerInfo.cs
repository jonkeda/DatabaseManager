using System.Collections.Generic;

namespace DatabaseManager.Model
{
    public class SchemaDesignerInfo
    {
        public List<TableColumnDesingerInfo> TableColumnDesingerInfos = new List<TableColumnDesingerInfo>();
        public List<TableConstraintDesignerInfo> TableConstraintDesignerInfos = new List<TableConstraintDesignerInfo>();
        public List<TableForeignKeyDesignerInfo> TableForeignKeyDesignerInfos = new List<TableForeignKeyDesignerInfo>();
        public List<TableIndexDesignerInfo> TableIndexDesingerInfos = new List<TableIndexDesignerInfo>();
        public bool IgnoreTableIndex { get; set; }
        public bool IgnoreTableForeignKey { get; set; }
        public bool IgnoreTableConstraint { get; set; }
        public TableDesignerInfo TableDesignerInfo { get; set; }
    }
}