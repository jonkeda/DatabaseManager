using System.Collections.Generic;
using Databases.Handlers.MySql;
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

        private static readonly SqlHandlerDictionary Handlers = new SqlHandlerDictionary();

        static SqlHandler()
        {
            RegisterHandler(new TSqlHandler());
            RegisterHandler(new PlSqlHandler());
            RegisterHandler(new MySqlHandler());
            RegisterHandler(new SqliteHandler());
            RegisterHandler(new PostgreSqlHandler());

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

    }

    public abstract class SqlHandler<TBF, TSF, TA, TBU>
        : SqlHandler
        where TBF : ScriptBuildFactory, new()
        where TSF : StatementScriptBuilder, new()
        where TA : SqlAnalyserBase
        where TBU : DbBackup, new()
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
