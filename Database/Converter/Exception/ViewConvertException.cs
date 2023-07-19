using System;
using DatabaseInterpreter.Model;

namespace DatabaseConverter.Core
{
    public class ViewConvertException : ConvertException
    {
        public ViewConvertException(Exception ex) : base(ex)
        {
        }

        public override string ObjectType => nameof(View);
    }
}