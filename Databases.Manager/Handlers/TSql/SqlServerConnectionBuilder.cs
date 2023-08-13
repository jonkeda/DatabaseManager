﻿using System.Text;
using Databases.Connection;
using Databases.Model.Connection;

namespace Databases.Handlers.TSql
{
    public class SqlServerConnectionBuilder : IConnectionBuilder
    {
        public string BuildConnectionString(ConnectionInfo connectionInfo)
        {
            var sb = new StringBuilder(
                $"Data Source={connectionInfo.Server};Initial Catalog={connectionInfo.Database};TrustServerCertificate=true;");

            if (connectionInfo.IntegratedSecurity)
            {
                sb.Append("Integrated Security=true;");
            }
            else
            {
                sb.Append($"User Id={connectionInfo.UserId};Password={connectionInfo.Password};");
            }

            return sb.ToString();
        }
    }
}