using System;
using System.IO;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public abstract class DbBackup
    {
        public string DefaultBackupFolderName = "Backup";

        public DbBackup()
        {
        }

        public DbBackup(BackupSetting setting, ConnectionInfo connectionInfo)
        {
            Setting = setting;
            ConnectionInfo = connectionInfo;
        }

        public BackupSetting Setting { get; set; }
        public ConnectionInfo ConnectionInfo { get; set; }

        public abstract string Backup();

        protected string CheckSaveFolder()
        {
            var saveFolder = Setting.SaveFolder;

            if (string.IsNullOrEmpty(saveFolder)) saveFolder = DefaultBackupFolderName;

            if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

            return saveFolder;
        }

        protected virtual string ZipFile(string backupFilePath, string zipFilePath)
        {
            if (File.Exists(backupFilePath))
            {
                FileHelper.Zip(backupFilePath, zipFilePath);

                if (File.Exists(zipFilePath))
                {
                    File.Delete(backupFilePath);

                    backupFilePath = zipFilePath;
                }
            }

            return backupFilePath;
        }

        public static DbBackup GetInstance(DatabaseType databaseType)
        {
            if (databaseType == DatabaseType.SqlServer) return new SqlServerBackup();
            if (databaseType == DatabaseType.Oracle)
                return new OracleBackup();
            if (databaseType == DatabaseType.MySql)
                return new MySqlBackup();
            if (databaseType == DatabaseType.Postgres) return new PostgresBackup();

            throw new NotImplementedException($"Not implemente for backup {databaseType}.");
        }
    }
}