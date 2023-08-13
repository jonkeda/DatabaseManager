using System.Collections.Generic;
using Databases.Model.DatabaseObject;

namespace Databases.Manager.Model.TableDesigner
{
    public class TableDesignerGenerateScriptsData
    {
        public Table Table { get; set; }
        public List<Databases.Model.Script.Script> Scripts { get; set; } = new List<Databases.Model.Script.Script>();
    }
}