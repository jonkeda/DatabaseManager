using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace Databases.SqlAnalyser.Model.DatabaseObject
{
    public class ForeignKeyInfo
    {
        public TableName TableName { get; set; }
        public List<ColumnName> ColumnNames { get; set; } = new List<ColumnName>();
        public TableName RefTableName { get; set; }
        public List<ColumnName> RefColumnNames { get; set; } = new List<ColumnName>();
        public bool UpdateCascade { get; set; }
        public bool DeleteCascade { get; set; }
    }
}