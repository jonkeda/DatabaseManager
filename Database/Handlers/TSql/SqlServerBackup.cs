using System;
using System.IO;
using Databases.Manager.Backup;
using Databases.Manager.Model.Setting;
using Databases.Model.Connection;
using Databases.Model.Option;

namespace Databases.Handlers.TSql
{
    public class SqlServerBackup : DbBackup
    {
        public SqlServerBackup()
        { }

        public SqlServerBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        { }

        public override string Backup()
        {
            if (Setting == null)
            {
                throw new ArgumentException("There is no backup setting for SqlServer.");
            }

            var fileNameWithoutExt = ConnectionInfo.Database + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = fileNameWithoutExt + ".bak";

            var saveFolder = CheckSaveFolder();

            var saveFilePath = Path.Combine(saveFolder, fileName);

            var interpreter = new SqlServerInterpreter(ConnectionInfo, new DbInterpreterOption());

            var sql = $@"use master; backup database {ConnectionInfo.Database} to disk='{saveFilePath}';";

            interpreter.ExecuteNonQueryAsync(sql);

            return saveFilePath;
        }
    }
}