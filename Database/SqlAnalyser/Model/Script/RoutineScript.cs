using System.Collections.Generic;
using Databases.SqlAnalyser.Model.DatabaseObject;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Script
{
    public class RoutineScript : CommonScript
    {
        public RoutineType Type { get; set; }

        public TokenInfo ReturnDataType { get; set; }

        public List<Parameter> Parameters { get; set; } = new List<Parameter>();

        public TableInfo ReturnTable { get; set; }
    }
}