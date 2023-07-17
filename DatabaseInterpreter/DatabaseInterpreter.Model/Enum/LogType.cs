using System;

namespace DatabaseInterpreter.Model
{
    [Flags]
    public enum LogType
    {
        None = 0,
        Info = 2,
        Error = 4
    }
}