using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;
using Microsoft.Data.Sqlite;

namespace DatabaseManager.Profile
{
    public class ProfileBaseManager
    {
        private static readonly string dataFileName = "profiles.db3";

        static ProfileBaseManager()
        {
            Init();
        }

        internal static string ProfileFolder => "Profiles";
        internal static string ProfileDataFile { get; private set; }

        private static void Init()
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var folder = Path.Combine(assemblyFolder, ProfileFolder);

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var dataFilePath = Path.Combine(folder, dataFileName);

            var templateFilePath = Path.Combine(assemblyFolder, "Template", dataFileName);

            if (!File.Exists(dataFilePath))
            {
                if (!File.Exists(templateFilePath))
                    throw new FileNotFoundException($@"File ""{templateFilePath}"" is not found.");

                File.Copy(templateFilePath, dataFilePath);

                ProfileDataFile = dataFilePath;
            }
            else
            {
                var templateVersion = GetVersion(templateFilePath);
                var dataVersion = GetVersion(dataFilePath);

                if (!string.IsNullOrEmpty(templateVersion) && !string.IsNullOrEmpty(dataVersion) &&
                    templateVersion != dataVersion) File.Copy(templateFilePath, dataFilePath, true);

                ProfileDataFile = dataFilePath;
            }
        }

        private static string GetVersion(string dataFilePath)
        {
            using (var connection = CreateDbConnection(dataFilePath))
            {
                connection.Open();

                var sql = "SELECT Version FROM VersionInfo";

                var cmd = connection.CreateCommand();

                cmd.CommandText = sql;

                return cmd.ExecuteScalar()?.ToString();
            }
        }

        private static ConnectionInfo GetConnectionInfo(string dataFilePath)
        {
            return new ConnectionInfo { Database = dataFilePath };
        }

        protected static ConnectionInfo GetConnectionInfo()
        {
            return GetConnectionInfo(ProfileDataFile);
        }

        protected static DbInterpreter GetDbInterpreter(string dataFilePath = null)
        {
            if (dataFilePath == null) dataFilePath = ProfileDataFile;

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.Sqlite,
                GetConnectionInfo(dataFilePath), new DbInterpreterOption());

            return dbInterpreter;
        }

        protected static SqliteConnection CreateDbConnection(string dataFilePath = null)
        {
            if (dataFilePath == null) dataFilePath = ProfileDataFile;

            return GetDbInterpreter(dataFilePath).CreateConnection() as SqliteConnection;
        }

        protected static object GetParameterValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return DBNull.Value;

            return value;
        }

        protected static bool ExistsProfileDataFile()
        {
            return File.Exists(ProfileDataFile);
        }

        protected static bool ValidateIds(IEnumerable<string> ids)
        {
            if (ids == null) return true;

            if (ids.Any(item => !Guid.TryParse(item, out _))) throw new ArgumentException("Invalid id exists.");

            return true;
        }
    }
}