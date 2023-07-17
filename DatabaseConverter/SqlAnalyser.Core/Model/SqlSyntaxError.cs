using System.Collections.Generic;
using System.Text;

namespace SqlAnalyser.Model
{
    public class SqlSyntaxError
    {
        public List<SqlSyntaxErrorItem> Items = new List<SqlSyntaxErrorItem>();
        public bool HasError => Items != null && Items.Count > 0;

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var item in Items)
                sb.AppendLine(
                    $"{item.Text}(Line={item.Line},Column={item.Column},StartIndex={item.StartIndex},StopIndex={item.StopIndex}):{item.Message};");

            return sb.ToString();
        }
    }

    public class SqlSyntaxErrorItem
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public int StartIndex { get; set; }
        public int StopIndex { get; set; }
        public string Text { get; set; }
        public string Message { get; set; }
    }
}