using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Handlers.PlSql
{
    public class OracleDiagnosis : DbDiagnosis
    {
        public OracleDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        { }

        public override DatabaseType DatabaseType => DatabaseType.Oracle;

        public override string GetStringLengthFunction()
        {
            return "LENGTH";
        }

        public override string GetStringNullFunction()
        {
            return "NVL";
        }
    }
}