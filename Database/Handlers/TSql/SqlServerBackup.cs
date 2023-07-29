using System;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;

namespace DatabaseManager.Core
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