﻿using System.Data.Common;
using Databases.Interpreter;
using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.ScriptGenerator;
using Databases.SqlAnalyser;
using Microsoft.Data.SqlClient;

namespace Databases.Handlers.TSql
{
    public class TSqlHandler : SqlHandler<
        TSqlScriptBuildFactory,
        TSqlStatementScriptBuilder,
        TSqlAnalyser,
        SqlServerBackup,
        SqlServerDiagnosis>
    {
        public TSqlHandler() : base(DatabaseType.SqlServer)
        { }


        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new TSqlAnalyser(content);
        }

        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new SqlServerDiagnosis(connectionInfo);
        }


        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new SqlServerInterpreter(connectionInfo, option);
        }

        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new SqlServerScriptGenerator(dbInterpreter);
        }

        protected override DbProviderFactory CreateDbProviderFactory(string providerName)
        {
            if (providerName.Contains("sqlclient"))
            {
                return SqlClientFactory.Instance;
            }

            return null;
        }
    }
}