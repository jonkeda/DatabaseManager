using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
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