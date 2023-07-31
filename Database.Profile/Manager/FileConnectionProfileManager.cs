using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseManager.Model;
using Databases.Interpreter.Builder;
using Databases.Interpreter.Utility.Helper;

namespace DatabaseManager.Profile
{
    public class FileConnectionProfileManager : ProfileBaseManager
    {
        public static async Task<IEnumerable<FileConnectionProfileInfo>> GetProfiles(string databaseType)
        {
            return await GetProfiles(new ProfileFilter { DatabaseType = databaseType });
        }

        public static async Task<FileConnectionProfileInfo> GetProfileById(string id)
        {
            return (await GetProfiles(new ProfileFilter { Id = id }))?.FirstOrDefault();
        }

        public static async Task<FileConnectionProfileInfo> GetProfileByDatabase(string databaseType, string database)
        {
            return (await GetProfiles(new ProfileFilter { DatabaseType = databaseType, Database = database }))
                ?.FirstOrDefault();
        }

        private static async Task<IEnumerable<FileConnectionProfileInfo>> GetProfiles(ProfileFilter filter)
        {
            var profiles = Enumerable.Empty<FileConnectionProfileInfo>();

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sb = new SqlBuilder();

                    sb.Append(
                        @"SELECT Id,DatabaseType,SubType,Database,DatabaseVersion,EncryptionType,HasPassword,Password,Name 
                                FROM FileConnection
                                WHERE 1=1"
                    );

                    var dbType = filter?.DatabaseType;
                    var id = filter?.Id;
                    var database = filter?.Database;

                    var para = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(dbType))
                    {
                        sb.Append("AND DatabaseType=@DbType");

                        para.Add("@DbType", filter.DatabaseType);
                    }

                    if (!string.IsNullOrEmpty(id))
                    {
                        sb.Append("AND Id=@Id");
                        para.Add("@Id", id);
                    }

                    if (!string.IsNullOrEmpty(database))
                    {
                        sb.Append("AND Database=@Database");

                        para.Add("@Database", database);
                    }

                    profiles = await connection.QueryAsync<FileConnectionProfileInfo>(sb.Content, para);

                    foreach (var profile in profiles)
                        if (!string.IsNullOrEmpty(profile.Password))
                            profile.Password = AesHelper.Decrypt(profile.Password);
                }

            return profiles;
        }

        public static async Task<string> Save(FileConnectionProfileInfo info, bool rememberPassword)
        {
            if (ExistsProfileDataFile() && info != null)
            {
                FileConnectionProfileInfo oldProfile = null;

                if (!string.IsNullOrEmpty(info.Id))
                    oldProfile = await GetProfileById(info.Id);
                else if (!string.IsNullOrEmpty(info.Database))
                    oldProfile = await GetProfileByDatabase(info.DatabaseType, info.Database);

                var password = info.Password;

                if (!string.IsNullOrEmpty(password) && rememberPassword)
                    password = AesHelper.Encrypt(password);
                else
                    password = null;

                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var trans = connection.BeginTransaction();

                    string id = null;

                    var result = -1;

                    if (oldProfile == null)
                    {
                        id = Guid.NewGuid().ToString();

                        var sql =
                            $@"INSERT INTO FileConnection(Id,DatabaseType,SubType,Database,DatabaseVersion,EncryptionType,HasPassword,Password,Name)
                                       VALUES('{id}',@DbType,@SubType,@Database,@DatabaseVersion,@EncryptionType,{ValueHelper.BooleanToInteger(info.HasPassword)},@Password,@Name)";

                        var cmd = connection.CreateCommand();
                        cmd.CommandText = sql;

                        cmd.Parameters.AddWithValue("@DbType", info.DatabaseType);
                        cmd.Parameters.AddWithValue("@SubType", GetParameterValue(info.SubType));
                        cmd.Parameters.AddWithValue("@Database", GetParameterValue(info.Database));
                        cmd.Parameters.AddWithValue("@DatabaseVersion", GetParameterValue(info.DatabaseVersion));
                        cmd.Parameters.AddWithValue("@EncryptionType", GetParameterValue(info.EncryptionType));
                        cmd.Parameters.AddWithValue("@Password", GetParameterValue(password));
                        cmd.Parameters.AddWithValue("@Name", GetParameterValue(info.Name));

                        result = await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        id = oldProfile.Id;

                        var sql =
                            @"UPDATE FileConnection SET SubType=SubType,Database=@Database,DatabaseVersion=@DatabaseVersion,
                                     EncryptionType=@EncryptionType,Password=@Password,Name=@Name
                                     WHERE ID=@Id";

                        var cmd = connection.CreateCommand();
                        cmd.CommandText = sql;

                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@SubType", GetParameterValue(info.SubType));
                        cmd.Parameters.AddWithValue("@Database", GetParameterValue(info.Database));
                        cmd.Parameters.AddWithValue("@DatabaseVersion", GetParameterValue(info.DatabaseVersion));
                        cmd.Parameters.AddWithValue("@EncryptionType", GetParameterValue(info.EncryptionType));
                        cmd.Parameters.AddWithValue("@Password", GetParameterValue(password));
                        cmd.Parameters.AddWithValue("@Name", GetParameterValue(info.Name));

                        result = await cmd.ExecuteNonQueryAsync();
                    }

                    if (result > 0)
                    {
                        trans.Commit();

                        return id;
                    }
                }
            }

            return string.Empty;
        }

        public static async Task<bool> Delete(IEnumerable<string> ids)
        {
            if (!ValidateIds(ids)) return false;

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var trans = connection.BeginTransaction();

                    var strIds = string.Join(",", ids.Select(item => $"'{item}'"));

                    var sql = $@"DELETE FROM FileConnection
                                    WHERE Id IN({strIds})";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;

                    var result = await cmd.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        trans.Commit();

                        return true;
                    }
                }

            return false;
        }
    }
}