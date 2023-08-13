using System;
using System.Collections.Generic;

namespace Databases.Converter.Model
{
    public class FunctionFormula
    {
        private string _body;
        private string _expression;
        private string _name;

        public FunctionFormula(string expression)
        {
            Expression = expression;
        }

        public FunctionFormula(string name, string expression)
        {
            Name = name;
            Expression = expression;
        }

        public bool HasParentheses => _expression?.Contains("(") == true;

        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_name) && !string.IsNullOrEmpty(_expression))
                {
                    var firstParenthesesIndex = Expression.IndexOf('(');

                    if (firstParenthesesIndex > 0)
                    {
                        _name = _expression.Substring(0, firstParenthesesIndex);
                    }
                }

                return _name;
            }
            set => _name = value;
        }

        public string Expression
        {
            get => _expression;
            set
            {
                _body = null;
                _expression = value;
            }
        }

        public string Body
        {
            get
            {
                if (string.IsNullOrEmpty(_body))
                {
                    if (!string.IsNullOrEmpty(Expression))
                    {
                        var firstParenthesesIndexIndex = Expression.IndexOf('(');
                        var lastParenthesesIndex = Expression.LastIndexOf(')');

                        if (firstParenthesesIndexIndex > 0 && lastParenthesesIndex > 0 &&
                            lastParenthesesIndex > firstParenthesesIndexIndex)
                        {
                            _body = Expression.Substring(firstParenthesesIndexIndex + 1,
                                lastParenthesesIndex - firstParenthesesIndexIndex - 1);
                        }
                    }
                }

                return _body ?? string.Empty;
            }
        }

        public List<string> GetArgs(string delimiter = ",")
        {
            var args = new List<string>();

            var body = Body;

            if (string.IsNullOrEmpty(body))
            {
                return args;
            }

            var delimiterIndexes = new List<int>();

            if (delimiter.Length == 1)
            {
                var delimiterChar = delimiter[0];

                var i = 0;

                var leftParenthesesCount = 0;
                var rightParenthesesCount = 0;
                var singleQuotationCharCount = 0;

                foreach (var c in body)
                {
                    if (c == '\'')
                    {
                        singleQuotationCharCount++;
                    }

                    if (c == '(')
                    {
                        if (singleQuotationCharCount % 2 == 0)
                        {
                            leftParenthesesCount++;
                        }
                    }
                    else if (c == ')')
                    {
                        if (singleQuotationCharCount % 2 == 0)
                        {
                            rightParenthesesCount++;
                        }
                    }

                    if (c == delimiterChar)
                    {
                        if (leftParenthesesCount == rightParenthesesCount && singleQuotationCharCount % 2 == 0)
                        {
                            delimiterIndexes.Add(i);
                        }
                    }

                    i++;
                }

                var lastDelimiterIndex = -1;

                foreach (var delimiterIndex in delimiterIndexes)
                {
                    var startIndex = lastDelimiterIndex == -1 ? 0 : lastDelimiterIndex + 1;
                    var length = delimiterIndex - startIndex;

                    if (length > 0)
                    {
                        var value = body.Substring(startIndex, length);

                        args.Add(value.Trim());
                    }

                    lastDelimiterIndex = delimiterIndex;
                }

                if (lastDelimiterIndex < body.Length - 1)
                {
                    args.Add(body.Substring(lastDelimiterIndex + 1).Trim());
                }
            }
            else
            {
                var lastIndex = body.LastIndexOf(delimiter, StringComparison.OrdinalIgnoreCase);

                if (lastIndex >= 0)
                {
                    var firstPart = body.Substring(0, lastIndex).Trim();
                    var lastPart = body.Substring(lastIndex + delimiter.Length).Trim();

                    args.Add(firstPart);
                    args.Add(lastPart);
                }
            }

            return args;
        }
    }
}