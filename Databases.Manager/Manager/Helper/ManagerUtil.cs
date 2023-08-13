using System;
using System.Diagnostics;
using Databases.Model.Enum;
using Humanizer;

namespace Databases.Manager.Helper
{
    public class ManagerUtil
    {
        public static DatabaseType GetDatabaseType(string dbType)
        {
            if (!string.IsNullOrEmpty(dbType))
            {
                return (DatabaseType)Enum.Parse(typeof(DatabaseType), dbType);
            }

            return DatabaseType.Unknown;
        }

        public static bool IsFileConnection(DatabaseType databaseType)
        {
            if (databaseType == DatabaseType.Sqlite)
            {
                return true;
            }

            return false;
        }

        public static void OpenInExplorer(string filePath)
        {
            var cmd = "explorer.exe";
            var arg = "/select," + filePath;
            Process.Start(cmd, arg);
        }

        public static string GetSingularString(string value)
        {
            return value.Singularize();
        }

        public static string GetPluralString(string value)
        {
            return value.Pluralize();
        }

        public static bool SupportComment(DatabaseType databaseType)
        {
            if (databaseType == DatabaseType.Sqlite)
            {
                return false;
            }

            return true;
        }
    }
}