namespace Databases.SqlAnalyser.Model.Statement
{
    public class UnionStatement : Statement, IStatementScriptBuilder
    {
        public UnionType Type { get; set; }
        public SelectStatement SelectStatement { get; set; }

        public void Build(FullStatementScriptBuilder builder)
        {
            builder.Builds(this);
        }
    }

    public enum UnionType
    {
        UNION = 0,
        UNION_ALL = 1,
        INTERSECT = 2,
        EXCEPT = 3,
        MINUS = 4
    }
}