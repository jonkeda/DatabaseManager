namespace SqlAnalyser.Model
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