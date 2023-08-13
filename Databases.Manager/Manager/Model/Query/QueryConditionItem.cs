using System;
using System.Collections.Generic;
using System.Linq;
using Databases.Manager.Helper;

namespace Databases.Manager.Model.Query
{
    public class QueryConditionItem
    {
        public string ColumnName { get; set; }
        public Type DataType { get; set; }
        public QueryConditionMode Mode { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public List<string> Values { get; set; } = new List<string>();
        public bool NeedQuoted => FrontQueryHelper.NeedQuotedForSql(DataType);

        private string GetValue(string value)
        {
            return NeedQuoted ? $"'{FrontQueryHelper.GetSafeValue(value)}'" : value;
        }

        public override string ToString()
        {
            var conditon = "";

            if (Mode == QueryConditionMode.Single)
            {
                var value = Operator.Contains("LIKE") ? $"'%{Value}%'" : GetValue(Value);

                conditon = $"{Operator} {value}";
            }
            else if (Mode == QueryConditionMode.Range)
            {
                conditon = $"BETWEEN {GetValue(From)} AND {GetValue(To)}";
            }
            else if (Mode == QueryConditionMode.Series)
            {
                conditon = $"IN({string.Join(",", Values.Select(item => GetValue(item)))})";
            }

            return conditon;
        }
    }

    public enum QueryConditionMode
    {
        Single = 0,
        Range = 1,
        Series = 2
    }
}