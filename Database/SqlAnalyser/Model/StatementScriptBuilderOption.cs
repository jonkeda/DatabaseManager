using System;
using System.Collections.Generic;

namespace SqlAnalyser.Model
{
    public class StatementScriptBuilderOption
    {
        public bool NotBuildDeclareStatement { get; set; }
        public bool CollectDeclareStatement { get; set; }
        public bool OutputRemindInformation { get; set; }
        public List<Type> CollectSpecialStatementTypes { get; set; } = new List<Type>();
    }
}