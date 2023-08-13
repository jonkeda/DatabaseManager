using System.Collections.Generic;
using System.Xml.Linq;
using Databases.Interpreter.Utility.Helper;

namespace Databases.Converter.Model.Mappings
{
    public class DataTypeMapping
    {
        public DataTypeMappingSource Source { get; set; }
        public DataTypeMappingTarget Target { get; set; }
        public List<DataTypeMappingSpecial> Specials { get; set; } = new List<DataTypeMappingSpecial>();
    }

    public class DataTypeMappingSource
    {
        public DataTypeMappingSource()
        { }

        public DataTypeMappingSource(XElement element)
        {
            var source = element.Element("source");
            Type = source.Attribute("type").Value;
            IsExpression = source.Attribute("isExp")?.Value == "true";
        }

        public string Type { get; set; }
        public bool IsExpression { get; set; }
    }

    public class DataTypeMappingTarget
    {
        public DataTypeMappingTarget()
        { }

        public DataTypeMappingTarget(XElement element)
        {
            var target = element.Element("target");
            Type = target.Attribute("type")?.Value;
            Length = target.Attribute("length")?.Value;
            Precision = target.Attribute("precision")?.Value;
            Scale = target.Attribute("scale")?.Value;
            Substitute = target.Attribute("substitute")?.Value;
            Args = target.Attribute("args")?.Value;
        }

        public string Type { get; set; }
        public string Length { get; set; }
        public string Precision { get; set; }
        public string Scale { get; set; }
        public string Substitute { get; set; }
        public string Args { get; set; }
        public List<DataTypeMappingArgument> Arguments { get; set; } = new List<DataTypeMappingArgument>();
    }

    public struct DataTypeMappingArgument
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class DataTypeMappingSpecial
    {
        public DataTypeMappingSpecial()
        { }

        public DataTypeMappingSpecial(XElement element)
        {
            Name = element.Attribute("name")?.Value;
            Value = element.Attribute("value")?.Value;
            Type = element.Attribute("type")?.Value;
            TargetMaxLength = element.Attribute("targetMaxLength")?.Value;
            Substitute = element.Attribute("substitute")?.Value;
            NoLength = ValueHelper.IsTrueValue(element.Attribute("noLength")?.Value);
            Precison = element.Attribute("precision")?.Value;
            Scale = element.Attribute("scale")?.Value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string TargetMaxLength { get; set; }
        public string Substitute { get; set; }
        public bool NoLength { get; set; }
        public string Precison { get; set; }
        public string Scale { get; set; }
    }
}