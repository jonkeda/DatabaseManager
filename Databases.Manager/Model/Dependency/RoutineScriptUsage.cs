using System.Collections.Generic;

namespace Databases.Model.Dependency
{
    public class RoutineScriptUsage : DbObjectUsage
    {
        public List<string> ColumnNames { get; set; } = new List<string>();
    }
}