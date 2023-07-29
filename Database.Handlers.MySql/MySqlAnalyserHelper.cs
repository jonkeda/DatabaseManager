using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public static class MySqlAnalyserHelper
    {
        public static void RearrangeStatements(List<Statement> statements)
        {
            FetchCursorStatement fetchCursorStatement = null;

            var statementsNeedToRemove = new List<FetchCursorStatement>();

            foreach (var statement in statements)
                if (statement is FetchCursorStatement fetch)
                {
                    fetchCursorStatement = fetch;
                }
                else if (statement is WhileStatement @while)
                {
                    var fs =
                        @while.Statements.FirstOrDefault(item => item is FetchCursorStatement) as FetchCursorStatement;

                    if (fetchCursorStatement != null && fs != null)
                    {
                        statementsNeedToRemove.Add(fetchCursorStatement);

                        @while.Condition.Symbol = "FINISHED = 0";

                        var index = @while.Statements.IndexOf(fs);

                        @while.Statements.Insert(0, fs);

                        @while.Statements.RemoveAt(index + 1);
                    }
                }

            statements.RemoveAll(item => statementsNeedToRemove.Contains(item));
        }

        public static UserVariableDataType DetectUserVariableDataType(string value)
        {
            if (DataTypeHelper.StartsWithN(value) || ValueHelper.IsStringValue(value))
                return UserVariableDataType.String;
            if (int.TryParse(value, out _))
                return UserVariableDataType.Integer;
            if (decimal.TryParse(value, out _)) return UserVariableDataType.Decimal;

            return UserVariableDataType.Unknown;
        }
    }
}