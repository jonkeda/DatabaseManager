using System.ComponentModel;
using Databases.Model.DatabaseObject;

namespace Databases.Manager.Model.TableDesigner
{
    public class TableColumnDesingerInfo : TableColumn
    {
        public string OldName { get; set; }
        public bool IsPrimary { get; set; }
        public string Length { get; set; }

        public TableColumnExtraPropertyInfo ExtraPropertyInfo { get; set; }
    }

    public class TableColumnExtraPropertyInfo
    {
        [Category("Identity")]
        [Description("Seed")]
        [ReadOnly(false)]
        public int Seed { get; set; } = 1;

        [Category("Identity")]
        [Description("Increment")]
        [ReadOnly(false)]
        public int Increment { get; set; } = 1;

        [Category("Compute")]
        [Description("Expression")]
        [ReadOnly(false)]
        public string Expression { get; set; }
    }
}