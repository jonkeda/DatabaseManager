using System;

namespace Databases.Model.Enum
{
    [Flags]
    public enum LogType
    {
        None = 0,
        Info = 2,
        Error = 4
    }
}