using System;
using System.Collections.Generic;
using System.Linq;
using Databases.Handlers;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;

namespace Databases.Interpreter.Helper
{
    public class DbInterpreterHelper
    {
        public static DbInterpreter GetDbInterpreter(DatabaseType dbType, ConnectionInfo connectionInfo,
            DbInterpreterOption option)
        {
            return SqlHandler.GetHandler(dbType).CreateDbInterpreter(connectionInfo, option);
        }

        public static IEnumerable<DatabaseType> GetDisplayDatabaseTypes()
        {
            return Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>()
                .Where(item => item != DatabaseType.Unknown);
        }
    }
}