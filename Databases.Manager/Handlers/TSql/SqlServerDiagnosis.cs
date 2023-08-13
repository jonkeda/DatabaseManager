using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Handlers.TSql
{
    public class SqlServerDiagnosis : DbDiagnosis
    {
        public SqlServerDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        { }

        public override DatabaseType DatabaseType => DatabaseType.SqlServer;

        public override string GetStringLengthFunction()
        {
            return "LEN";
        }

        public override string GetStringNullFunction()
        {
            return "ISNULL";
        }
    }
}