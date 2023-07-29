using System.Collections.Generic;
using System.Data.Common;
using DatabaseInterpreter.Core;
//using Databases.Handlers.MySql;
using Databases.Handlers.PlSql;
using Databases.Handlers.Sqlite;
using Databases.Handlers.TSql;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using SqlAnalyser.Core;

namespace Databases.Handlers
{
    public class SqlHandlerDictionary : Dictionary<DatabaseType, SqlHandler>
    {

    }

    public abstract class SqlHandler
    {
        protected SqlHandler(DatabaseType databaseType)
        {
            DatabaseType = databaseType;
        }

        public DatabaseType DatabaseType { get; }

        public abstract ScriptBuildFactory CreateScriptBuildFactory();
        public abstract StatementScriptBuilder CreateStatementScriptBuilder();
        public abstract SqlAnalyserBase GetSqlAnalyser(string content);
        public abstract DbBackup CreateDbBackup();
        public abstract DbDiagnosis CreateDbDiagnosis(ConnectionInfo connectionInfo);
        public abstract DbInterpreter CreateDbInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option);
        public abstract DbScriptGenerator CreateDbScriptGenerator(DbInterpreter dbInterpreter);
        protected abstract DbProviderFactory CreateDbProviderFactory(string providerName);

        private static readonly SqlHandlerDictionary Handlers = new SqlHandlerDictionary();

        static SqlHandler()
        {

        }

        public static void RegisterHandler(SqlHandler sqlHandler)
        {
            Handlers.Add(sqlHandler.DatabaseType, sqlHandler);
        }

        public static SqlHandler GetHandler(DatabaseType databaseType)
        {
            if (Handlers.TryGetValue(databaseType, out var handler))
            {
                return handler;
            }

            throw new KeyNotFoundException($"The handler for {databaseType} is not found.");
        }

        public static DbProviderFactory CreateConnection(string providerName)
        {
            foreach (var handler in Handlers.Values)
            {
                var provider = handler.CreateDbProviderFactory(providerName);
                if (provider != null) 
                    return provider;  
            }

            return null;
        }

    }

    public abstract class SqlHandler<TBF, TSF, TA, TBU, TD>
        : SqlHandler
        where TBF : ScriptBuildFactory, new()
        where TSF : StatementScriptBuilder, new()
        where TA : SqlAnalyserBase
        where TBU : DbBackup, new()
        where TD : DbDiagnosis
    {
        protected SqlHandler(DatabaseType databaseType) : base(databaseType)
        {
        }

        public override ScriptBuildFactory CreateScriptBuildFactory()
        {
            return new TBF();
        }

        public override StatementScriptBuilder CreateStatementScriptBuilder()
        {
            return new TSF();
        }

        public override DbBackup CreateDbBackup()
        {
            return new TBU();
        }

    }
}
