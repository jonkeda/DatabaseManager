namespace DatabaseManager.Model
{
    public class QueryResult
    {
        public object Result;
        public QueryResultType ResultType { get; set; }
        public bool HasError { get; set; }
        public bool DoNothing { get; set; }
    }

    public enum QueryResultType
    {
        Unknown = 0,
        Grid = 1,
        Text = 2
    }
}