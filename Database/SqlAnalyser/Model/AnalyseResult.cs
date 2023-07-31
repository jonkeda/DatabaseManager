using Databases.SqlAnalyser.Model.Script;

namespace Databases.SqlAnalyser.Model
{
    public class AnalyseResult
    {
        public SqlSyntaxError Error { get; set; }
        public bool HasError => Error != null;
        public CommonScript Script { get; set; }
    }
}