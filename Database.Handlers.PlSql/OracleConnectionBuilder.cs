﻿using System.Text;
using Databases.Connection;
using Databases.Model.Connection;

namespace Databases.Handlers.PlSql
{
    public class OracleConnectionBuilder : IConnectionBuilder
    {
        public string BuildConnectionString(ConnectionInfo connectionInfo)
        {
            var server = connectionInfo.Server;
            var serviceName = OracleInterpreter.DEFAULT_SERVICE_NAME;
            var port = connectionInfo.Port;

            if (string.IsNullOrEmpty(port))
            {
                port = OracleInterpreter.DEFAULT_PORT.ToString();
            }

            if (server != null && server.Contains("/"))
            {
                var serverService = connectionInfo.Server.Split('/');
                server = serverService[0];
                serviceName = serverService[1];
            }

            var sb = new StringBuilder(
                $"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={server})(PORT={port})))(CONNECT_DATA=(SERVICE_NAME={serviceName})));");

            if (connectionInfo.IntegratedSecurity)
            {
                sb.Append("User Id=/;");
            }
            else
            {
                sb.Append($"User Id={connectionInfo.UserId};Password={connectionInfo.Password};");
            }

            if (connectionInfo.IsDba)
            {
                sb.Append("DBA PRIVILEGE=SYSDBA;");
            }

            return sb.ToString();
        }
    }
}