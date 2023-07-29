using Databases.SqlAnalyser.Model;
using Databases.SqlAnalyser.Model.Script;

namespace SqlAnalyser.Model
{
    public class AnalyseResult
    {
        public SqlSyntaxError Error { get; set; }
        public bool HasError => Error != null;
        public CommonScript Script { get; set; }
    }
}