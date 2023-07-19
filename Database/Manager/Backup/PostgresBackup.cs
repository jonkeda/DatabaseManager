using System;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class PostgresBackup : DbBackup
    {
        public PostgresBackup()
        {
        }

        public PostgresBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        {
        }

        public override string Backup()
        {
            if (Setting == null) throw new ArgumentException("There is no backup setting for Postgres.");

            var exeFilePath = Setting.ClientToolFilePath;

            if (string.IsNullOrEmpty(exeFilePath)) throw new ArgumentNullException("client backup file path is empty.");

            if (!File.Exists(exeFilePath))
                throw new ArgumentException($"The backup file path is not existed:{Setting.ClientToolFilePath}.");
            if (Path.GetFileName(exeFilePath).ToLower() != "pg_dump.exe")
                throw new ArgumentException("The backup file should be pg_dump.exe");

            var server = ConnectionInfo.Server;
            var port = string.IsNullOrEmpty(ConnectionInfo.Port)
                ? PostgresInterpreter.DEFAULT_PORT.ToString()
                : ConnectionInfo.Port;
            var database = ConnectionInfo.Database;
            var userName = ConnectionInfo.UserId;
            var password = ConnectionInfo.Password;
            var strPassword = ConnectionInfo.IntegratedSecurity ? "" : $":{password}";

            var fileNameWithoutExt = ConnectionInfo.Database + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = fileNameWithoutExt + ".tar";

            var saveFolder = CheckSaveFolder();

            var saveFilePath = Path.Combine(saveFolder, fileName);

            var cmdArgs =
                $@"--dbname=postgresql://{userName}{strPassword}@{server}:{port}/{database}  -Ft -f ""{saveFilePath}""";

            var dumpFilePath = Path.Combine(Path.GetDirectoryName(Setting.ClientToolFilePath), "pg_dump.exe");

            var result = ProcessHelper.RunExe(dumpFilePath, cmdArgs, new[] { "exit" });

            if (!string.IsNullOrEmpty(result)) throw new Exception(result);

            return saveFilePath;
        }
    }
}