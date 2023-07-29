using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class CallStatement : Statement
    {
        public bool IsExecuteSql { get; set; }
        public TokenInfo Name { get; set; }
        public List<CallParameter> Parameters { get; set; } = new List<CallParameter>();
    }
}