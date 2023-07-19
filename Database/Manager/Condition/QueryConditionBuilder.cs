using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class QueryConditionBuilder
    {
        public DatabaseType DatabaseType { get; set; }
        public char QuotationLeftChar { get; set; }
        public char QuotationRightChar { get; set; }

        public List<QueryConditionItem> Conditions { get; } = new List<QueryConditionItem>();

        public void Add(QueryConditionItem condition)
        {
            Conditions.Add(condition);
        }

        public override string ToString()
        {
            return string.Join(" AND ", Conditions.Select(item => $"({GetConditionItemValue(item)})"));
        }

        private string GetConditionItemValue(QueryConditionItem item)
        {
            var typeConvert = "";

            if (item.NeedQuoted)
                if (DatabaseType == DatabaseType.Postgres)
                    typeConvert = "::CHARACTER VARYING ";

            var value = $"{QuotationLeftChar}{item.ColumnName}{QuotationRightChar}{typeConvert}{item}";

            return value;
        }
    }
}