using System;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
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