using System;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases;

namespace DatabaseManager.Core
{
    public class MySqlBackup : DbBackup
    {
        public MySqlBackup()
        {
        }

        public MySqlBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        {
        }

        public override string Backup()
        {
            if (Setting == null) throw new ArgumentException("There is no backup setting for MySql.");

            var exeFilePath = Setting.ClientToolFilePath;

            if (string.IsNullOrEmpty(exeFilePath)) throw new ArgumentNullException("client backup file path is empty.");

            if (!File.Exists(exeFilePath))
                throw new ArgumentException($"The backup file path is not existed:{Setting.ClientToolFilePath}.");
            if (Path.GetFileName(exeFilePath).ToLower() != "mysqldump.exe")
                throw new ArgumentException("The backup file should be mysqldump.exe");

            var fileNameWithoutExt = ConnectionInfo.Database + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = fileNameWithoutExt + ".sql";

            var saveFolder = CheckSaveFolder();

            var saveFilePath = Path.Combine(saveFolder, fileName);

            var server = ConnectionInfo.Server;
            var port = string.IsNullOrEmpty(ConnectionInfo.Port)
                ? MySqlInterpreter.DEFAULT_PORT.ToString()
                : ConnectionInfo.Port;
            var userId = ConnectionInfo.UserId;
            var password = ConnectionInfo.Password;
            var database = ConnectionInfo.Database;
            var charset = SettingManager.Setting.MySqlCharset;
            var skipQuotationNames = SettingManager.Setting.DbObjectNameMode == DbObjectNameMode.WithoutQuotation
                ? "--skip-quote-names"
                : "";

            var cmdArgs =
                $"--quick --default-character-set={charset} {skipQuotationNames} --lock-tables --force --host={server}  --port={port} --user={userId} --password={password} {database} -r \"{saveFilePath}\"";

            ProcessHelper.RunExe(Setting.ClientToolFilePath, cmdArgs);

            var zipFilePath = Path.Combine(saveFolder, fileNameWithoutExt + ".zip");

            if (Setting.ZipFile) saveFilePath = ZipFile(saveFilePath, zipFilePath);

            return saveFilePath;
        }
    }
}