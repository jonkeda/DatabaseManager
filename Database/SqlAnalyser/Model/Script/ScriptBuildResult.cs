﻿namespace Databases.SqlAnalyser.Model.Script
{
    public class ScriptBuildResult
    {
        public string Script { get; set; }
        public int BodyStartIndex { get; set; }
        public int BodyStopIndex { get; set; }
    }
}