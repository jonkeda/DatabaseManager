using System;
using Databases.Model.DatabaseObject;

namespace Databases.Exceptions
{
    public class ViewConvertException : ConvertException
    {
        public ViewConvertException(Exception ex) : base(ex)
        { }

        public override string ObjectType => nameof(View);
    }
}