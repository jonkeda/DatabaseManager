﻿using System.Text;
using DatabaseInterpreter.Model;
using Databases.Interpreter.Builder;

namespace DatabaseInterpreter.Core
{
    public class PostgresConnectionBuilder : IConnectionBuilder
    {
        public string BuildConnectionString(ConnectionInfo connectionInfo)
        {
            var server = connectionInfo.Server;
            var port = connectionInfo.Port;
            var timeout = DbInterpreter.Setting.CommandTimeout;

            if (string.IsNullOrEmpty(port)) port = PostgresInterpreter.DEFAULT_PORT.ToString();

            var sb = new StringBuilder(
                $"Host={server};Port={port};Database={connectionInfo.Database};CommandTimeout={timeout};");

            if (connectionInfo.IntegratedSecurity)
                sb.Append($"Integrated Security=True;Username={connectionInfo.UserId};");
            else
                sb.Append($"Username={connectionInfo.UserId};Password={connectionInfo.Password};");

            return sb.ToString();
        }
    }
}