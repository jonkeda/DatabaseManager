using System.Collections.Generic;

namespace Databases.Converter.Model
{
    public class DbConvertResult
    {
        public DbConvertResultInfoType InfoType { get; internal set; }
        public string Message { get; internal set; }
        public List<TranslateResult> TranslateResults { get; set; } = new List<TranslateResult>();
    }

    public enum DbConvertResultInfoType
    {
        Information = 0,
        Warning = 1,
        Error = 2
    }
}