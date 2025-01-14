﻿using System.IO;
using Databases.Handlers;
using Databases.Manager.Helper;
using Databases.Manager.Model.Setting;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace Databases.Manager.Backup
{
    public abstract class DbBackup
    {
        public string DefaultBackupFolderName = "Backup";

        protected DbBackup()
        { }

        protected DbBackup(BackupSetting setting, ConnectionInfo connectionInfo)
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

            if (string.IsNullOrEmpty(saveFolder))
            {
                saveFolder = DefaultBackupFolderName;
            }

            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

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
            return SqlHandler.GetHandler(databaseType).CreateDbBackup();
        }
    }
}