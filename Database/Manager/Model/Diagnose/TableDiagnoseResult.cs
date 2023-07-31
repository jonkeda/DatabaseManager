﻿using System.Collections.Generic;
using Databases.Model.DatabaseObject;

namespace Databases.Manager.Model.Diagnose
{
    public class TableDiagnoseResult
    {
        public List<TableDiagnoseResultDetail> Details { get; set; } = new List<TableDiagnoseResultDetail>();
    }

    public class TableDiagnoseResultDetail
    {
        public DatabaseObject DatabaseObject { get; set; }
        public int RecordCount { get; set; }
        public string Sql { get; set; }
    }

    public enum TableDiagnoseType
    {
        None = 0,
        NotNullWithEmpty = 1,
        SelfReferenceSame = 2,
        WithLeadingOrTrailingWhitespace = 3
    }
}