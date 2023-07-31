using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Handlers.Sqlite
{
    public class SqliteDiagnosis : DbDiagnosis
    {
        public SqliteDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        { }

        public override DatabaseType DatabaseType => DatabaseType.Sqlite;

        public override string GetStringLengthFunction()
        {
            return "LENGTH";
        }

        public override string GetStringNullFunction()
        {
            return "ISNULL";
        }
    }
}