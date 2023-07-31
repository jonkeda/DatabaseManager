using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Handlers.MySql
{
    public class MySqlDiagnosis : DbDiagnosis
    {
        public MySqlDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        { }

        public override DatabaseType DatabaseType => DatabaseType.MySql;

        public override string GetStringLengthFunction()
        {
            return "LENGTH";
        }

        public override string GetStringNullFunction()
        {
            return "IFNULL";
        }
    }
}