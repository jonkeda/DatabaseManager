﻿using System.Collections.Generic;
using Databases.Model.Enum;

namespace Databases.Manager.Model.Setting
{
    public class Setting : DbInterpreterSetting
    {
        public bool UseOriginalDataTypeIfUdtHasOnlyOneAttr { get; set; } = true;
        public DatabaseType PreferredDatabase { get; set; }
        public bool RememberPasswordDuringSession { get; set; } = true;
        public bool EnableEditorHighlighting { get; set; } = true;
        public bool EnableEditorIntellisence { get; set; } = true;
        public string ScriptsDefaultOutputFolder { get; set; }
        public bool ValidateScriptsAfterTranslated { get; set; } = true;
        public List<string> ConvertConcatCharTargetDatabases { get; set; } = new List<string>();
    }
}