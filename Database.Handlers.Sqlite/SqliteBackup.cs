using System;
using Databases.Manager.Backup;
using Databases.Manager.Model.Setting;
using Databases.Model.Connection;

namespace Databases.Handlers.Sqlite
{
    public class SqliteBackup : DbBackup
    {
        public SqliteBackup()
        { }

        public SqliteBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        { }

        public override string Backup()
        {
            throw new NotImplementedException();
        }
    }
}