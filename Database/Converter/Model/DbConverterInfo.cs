using System.Collections.Generic;
using Databases.Interpreter;
using Databases.Model.Schema;

namespace Databases.Converter.Model
{
    public class DbConverterInfo
    {
        public DatabaseObjectType DatabaseObjectType = DatabaseObjectType.None;

        public Dictionary<string, string> TableNameMappings = new Dictionary<string, string>();
        public DbInterpreter DbInterpreter { get; set; }
    }
}