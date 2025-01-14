﻿using System.Collections.Generic;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Schema;

namespace Databases.Manager.Model.Diagnose
{
    public class ScriptDiagnoseResult
    {
        public ScriptDbObject DbObject { get; set; }
        public List<ScriptDiagnoseResultDetail> Details { get; set; } = new List<ScriptDiagnoseResultDetail>();
    }

    public class ScriptDiagnoseResultDetail
    {
        public DatabaseObjectType ObjectType { get; set; }
        public string Name { get; set; }
        public string InvalidName { get; set; }
        public int Index { get; set; }
    }

    public enum ScriptDiagnoseType
    {
        None = 0,
        ViewColumnAliasWithoutQuotationChar = 1,
        NameNotMatch = 2
    }
}