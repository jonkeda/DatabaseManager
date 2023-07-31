using System.Collections.Generic;
using Databases.Model.DatabaseObject;

namespace Databases.Manager.Model
{
    public class ScriptGenerateResult
    {
        public List<RoutineParameter> Parameters;
        public string Script { get; set; }
    }
}