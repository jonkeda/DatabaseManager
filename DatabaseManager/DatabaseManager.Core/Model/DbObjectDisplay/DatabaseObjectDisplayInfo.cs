﻿using System.Collections.Generic;
using DatabaseInterpreter.Model;

namespace DatabaseManager.Model
{
    public class DatabaseObjectDisplayInfo
    {
        public ScriptAction ScriptAction;
        public List<RoutineParameter> ScriptParameters;
        public string Schema { get; set; }
        public string Name { get; set; }
        public DatabaseObject DatabaseObject { get; set; }
        public DatabaseObjectDisplayType DisplayType { get; set; } = DatabaseObjectDisplayType.Script;
        public DatabaseType DatabaseType { get; set; }
        public string Content { get; set; }
        public ConnectionInfo ConnectionInfo { get; set; }
        public object Error { get; set; }
        public bool IsNew { get; set; }
        public string FilePath { get; set; }
        public bool IsTranlatedScript { get; set; }
    }

    public enum DatabaseObjectDisplayType
    {
        Script = 0,
        Data = 1,
        TableDesigner = 2
    }
}