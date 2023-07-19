using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    public class PostgresDiagnosis : DbDiagnosis
    {
        public PostgresDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        {
        }

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