using System;
using DatabaseInterpreter.Model;

namespace DatabaseConverter.Core
{
    public class DataTransferException : ConvertException
    {
        public DataTransferException(Exception ex) : base(ex)
        {
        }

        public override string ObjectType => nameof(Table);
    }
}