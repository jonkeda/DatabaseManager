using System.Collections.Generic;

namespace Databases.Converter.Model
{
    public class TokenSymbolItemInfo
    {
        public List<TokenSymbolItemInfo> Children = new List<TokenSymbolItemInfo>();
        public int Index { get; set; }
        public string Content { get; set; }
        public TokenSymbolItemType Type { get; set; } = TokenSymbolItemType.Content;
    }

    public enum TokenSymbolItemType
    {
        Content,
        Keyword
    }
}