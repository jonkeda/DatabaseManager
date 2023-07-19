using System.Collections.Generic;
using DatabaseInterpreter.Model;

namespace DatabaseManager.Model
{
    public class ScriptGenerateResult
    {
        public List<RoutineParameter> Parameters;
        public string Script { get; set; }
    }
}