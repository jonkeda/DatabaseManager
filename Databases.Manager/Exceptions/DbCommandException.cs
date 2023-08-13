﻿using System;

namespace Databases.Exceptions
{
    public class DbCommandException : Exception
    {
        public DbCommandException(Exception ex)
        {
            BaseException = ex;
        }

        public DbCommandException(Exception ex, string msg)
        {
            BaseException = ex;
            CustomMessage = msg;
        }

        public Exception BaseException { get; internal set; }

        public string CustomMessage { get; internal set; }

        public bool HasRolledBackTransaction { get; internal set; }

        public override string Message => $"{BaseException?.Message}{Environment.NewLine}{CustomMessage}";
    }
}