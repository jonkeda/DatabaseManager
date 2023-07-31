using Databases.Manager.Diagnosis;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Handlers.PostgreSql
{
    public class PostgresDiagnosis : DbDiagnosis
    {
        public PostgresDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        { }

        public override DatabaseType DatabaseType => DatabaseType.Postgres;

        public override string GetStringLengthFunction()
        {
            return "LENGTH";
        }

        public override string GetStringNullFunction()
        {
            return "COALESCE";
        }
    }
}