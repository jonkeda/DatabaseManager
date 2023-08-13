using System.Collections.Generic;
using Databases.Model.DatabaseObject;

namespace Databases.Model.DataTable
{
    public class TableDataReadInfo
    {
        public Table Table { get; set; }
        public List<TableColumn> Columns { get; set; }
        public long TotalCount { get; set; }
        public List<Dictionary<string, object>> Data { get; set; }
        public System.Data.DataTable DataTable { get; set; }
    }
}