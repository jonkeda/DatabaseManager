﻿using System.Data.Common;
using Databases.Interpreter;
using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.ScriptGenerator;
using Databases.SqlAnalyser;
using Microsoft.Data.Sqlite;

namespace Databases.Handlers.Sqlite
{
    public class SqliteHandler : SqlHandler<
        SqliteScriptBuildFactory,
        SqliteStatementScriptBuilder,
        SqliteAnalyser,
        SqliteBackup,
        SqliteDiagnosis>
    {
        public SqliteHandler() : base(DatabaseType.Sqlite)
        { }

        public override SqlAnalyserBase GetSqlAnalyser(string content)
        {
            return new SqliteAnalyser(content);
        }

        public override DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo)
        {
            return new SqliteDiagnosis(connectionInfo);
        }


        public override DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return new SqliteInterpreter(connectionInfo, option);
        }

        public override DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return new SqliteScriptGenerator(dbInterpreter);
        }

        protected override DbProviderFactory CreateDbProviderFactory(string providerName)
        {
            if (providerName.Contains("sqlite"))
            {
                return SqliteFactory.Instance;
            }

            return null;
        }
    }
}