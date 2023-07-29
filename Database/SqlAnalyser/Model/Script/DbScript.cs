using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace Databases.SqlAnalyser.Model.Script
{
    public class DbScript
    {
        public string Schema { get; set; }
        public TokenInfo Name { get; set; }

        public string NameWithSchema
        {
            get
            {
                if (string.IsNullOrEmpty(Schema))
                {
                    return Name?.ToString();
                }

                return $"{Schema}.{Name}";
            }
        }
    }
}