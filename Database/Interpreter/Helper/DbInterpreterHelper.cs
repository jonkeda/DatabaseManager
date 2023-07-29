using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Model;
using Databases.Handlers;

namespace DatabaseInterpreter.Core
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