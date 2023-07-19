using System;

namespace DatabaseConverter.Core
{
    public class ScriptConvertException<T> : ConvertException
    {
        public ScriptConvertException(Exception ex) : base(ex)
        {
        }

        public override string ObjectType => typeof(T).Name;
    }
}