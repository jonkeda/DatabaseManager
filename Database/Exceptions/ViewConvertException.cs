using System;
using DatabaseInterpreter.Model;

namespace Databases.Exceptions
{
    public class ViewConvertException : ConvertException
    {
        public ViewConvertException(Exception ex) : base(ex)
        { }

        public override string ObjectType => nameof(View);
    }
}