using System;
using Databases.Model.DatabaseObject;

namespace Databases.Exceptions
{
    public class SchemaTransferException : ConvertException
    {
        public SchemaTransferException(Exception ex) : base(ex)
        { }

        public override string ObjectType => nameof(DatabaseObject);
    }
}