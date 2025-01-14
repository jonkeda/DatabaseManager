﻿using System;
using DatabaseInterpreter.Model;

namespace DatabaseConverter.Core
{
    public class SchemaTransferException : ConvertException
    {
        public SchemaTransferException(Exception ex) : base(ex)
        {
        }

        public override string ObjectType => nameof(DatabaseObject);
    }
}