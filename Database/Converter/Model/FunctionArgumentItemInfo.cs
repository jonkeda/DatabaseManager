using System.Collections.Generic;

namespace DatabaseConverter.Model
{
    public class FunctionArgumentItemInfo
    {
        public List<FunctionArgumentItemDetailInfo> Details = new List<FunctionArgumentItemDetailInfo>();
        public int Index { get; set; }
        public string Content { get; set; }
    }

    public class FunctionArgumentItemDetailInfo
    {
        public FunctionArgumentItemDetailType Type { get; set; }
        public string Content { get; set; }
    }

    public enum FunctionArgumentItemDetailType
    {
        Text,
        Whitespace
    }
}