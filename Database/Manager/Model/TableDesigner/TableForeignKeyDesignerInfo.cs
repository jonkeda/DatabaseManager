using Databases.Model.DatabaseObject;

namespace Databases.Manager.Model.TableDesigner
{
    public class TableForeignKeyDesignerInfo : TableForeignKey
    {
        public string OldName { get; set; }
    }
}