using System;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class SqliteBackup : DbBackup
    {
        public SqliteBackup()
        {
        }

        public SqliteBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        {
        }

        public override string Backup()
        {
            throw new NotImplementedException();   
        }
    }
}