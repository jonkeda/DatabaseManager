using System;
using System.Collections.Generic;
using System.Linq;

namespace Databases.Interpreter.Builder
{
    public class SqlBuilder
    {
        private readonly List<string> lines = new List<string>();
        public string Content => ToString();

        public void Append(string sql)
        {
            var items = sql.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in items)
            {
                var line = item.Trim();

                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, lines.Select(item => item));
        }
    }
}