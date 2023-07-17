using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    public class MySqlDiagnosis : DbDiagnosis
    {
        public MySqlDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo)
        {
        }

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