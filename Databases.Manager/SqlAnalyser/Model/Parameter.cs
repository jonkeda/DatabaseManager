using System;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model
{
    public class Parameter
    {
        public TokenInfo Name { get; set; }
        public ParameterType ParameterType { get; set; }
        public TokenInfo DataType { get; set; }
        public TokenInfo DefaultValue { get; set; }
    }

    [Flags]
    public enum ParameterType
    {
        NONE = 0,
        IN = 2,
        OUT = 4
    }
}