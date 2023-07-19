using System.Collections.Generic;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;

namespace DatabaseConverter.Model
{
    public class DbConveterInfo
    {
        public DatabaseObjectType DatabaseObjectType = DatabaseObjectType.None;

        public Dictionary<string, string> TableNameMappings = new Dictionary<string, string>();
        public DbInterpreter DbInterpreter { get; set; }
    }
}