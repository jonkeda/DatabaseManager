using System;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class OracleBackup : DbBackup
    {
        public OracleBackup()
        {
        }

        public OracleBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo)
        {
        }

        public override string Backup()
        {
            if (Setting == null) throw new ArgumentException("There is no backup setting for Oracle.");

            var exeFilePath = Setting.ClientToolFilePath;

            if (string.IsNullOrEmpty(exeFilePath)) throw new ArgumentNullException("client backup file path is empty.");

            if (!File.Exists(exeFilePath))
                throw new ArgumentException($"The backup file path is not existed:{Setting.ClientToolFilePath}.");
            if (Path.GetFileName(exeFilePath).ToLower() != "exp.exe")
                throw new ArgumentException("The backup file should be exp.exe");

            var server = ConnectionInfo.Server;
            var port = string.IsNullOrEmpty(ConnectionInfo.Port)
                ? OracleInterpreter.DEFAULT_PORT.ToString()
                : ConnectionInfo.Port;

            var serviceName = OracleInterpreter.DEFAULT_SERVICE_NAME;

            if (server != null && server.Contains("/"))
            {
                var serverService = server.Split('/');
                server = serverService[0];
                serviceName = serverService[1];
            }

            var connectArgs = "";

            if (ConnectionInfo.IntegratedSecurity)
                connectArgs = "/";
            else
                connectArgs =
                    $"{ConnectionInfo.UserId}/{ConnectionInfo.Password}@{server}:{port}/{serviceName} OWNER={ConnectionInfo.UserId}";

            if (ConnectionInfo.IsDba) connectArgs += " AS SYSDBA";

            var cmdArgs = $"-L -S {connectArgs} FULL=Y DIRECT=Y";

            var sqlplusFilePath = Path.Combine(Path.GetDirectoryName(Setting.ClientToolFilePath), "sqlplus.exe");

            var output = ProcessHelper.RunExe(sqlplusFilePath, cmdArgs, new[] { "exit" });

            if (!string.IsNullOrEmpty(output) && output.ToUpper().Contains("ERROR"))
                throw new Exception("Login failed.");

            var fileNameWithoutExt = ConnectionInfo.Database + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = fileNameWithoutExt + ".dmp";

            var saveFolder = CheckSaveFolder();

            var saveFilePath = Path.Combine(saveFolder, fileName);

            cmdArgs = $"{connectArgs} file='{saveFilePath}'";

            ProcessHelper.RunExe(Setting.ClientToolFilePath, cmdArgs);

            var zipFilePath = Path.Combine(saveFolder, fileNameWithoutExt + ".zip");

            if (Setting.ZipFile) saveFilePath = ZipFile(saveFilePath, zipFilePath);

            return saveFilePath;
        }
    }
}