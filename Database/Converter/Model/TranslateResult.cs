using Databases.Model.Schema;
using Databases.SqlAnalyser.Model;

namespace Databases.Converter.Model
{
    public class TranslateResult
    {
        public DatabaseObjectType DbObjectType { get; set; } = DatabaseObjectType.None;
        public string DbObjectSchema { get; set; }
        public string DbObjectName { get; set; }
        public SqlSyntaxError Error { get; set; }
        public string Data { get; set; }

        public bool HasError => Error != null;
    }
}