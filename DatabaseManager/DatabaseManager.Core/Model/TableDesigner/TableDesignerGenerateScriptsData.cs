using System.Collections.Generic;
using DatabaseInterpreter.Model;

namespace DatabaseManager.Model
{
    public class TableDesignerGenerateScriptsData
    {
        public Table Table { get; set; }
        public List<Script> Scripts { get; set; } = new List<Script>();
    }
}