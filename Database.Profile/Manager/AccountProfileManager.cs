using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Databases.Interpreter.Builder;
using Databases.Interpreter.Utility.Helper;
using Microsoft.Data.Sqlite;

namespace DatabaseManager.Profile
{
    public class AccountProfileManager : ProfileBaseManager
    {
        public static async Task<IEnumerable<AccountProfileInfo>> GetProfiles(string dbType)
        {
            var profiles = Enumerable.Empty<AccountProfileInfo>();

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sql =
                        @"SELECT Id,DatabaseType,Server,ServerVersion,Port,IntegratedSecurity,UserId,Password,IsDba,Usessl 
                                    FROM Account
                                    WHERE DatabaseType=@DbType";

                    var para = new Dictionary<string, object> { { "@DbType", dbType } };

                    profiles = await connection.QueryAsync<AccountProfileInfo>(sql, para);

                    foreach (var profile in profiles)
                        if (!profile.IntegratedSecurity && !string.IsNullOrEmpty(profile.Password))
                            profile.Password = AesHelper.Decrypt(profile.Password);
                }

            return profiles;
        }

        public static async Task<AccountProfileInfo> GetProfile(string dbType, string server, string port,
            bool integratedSecurity, string userId)
        {
            AccountProfileInfo profile = null;

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sb = new SqlBuilder();

                    sb.Append(
                        @"SELECT Id,DatabaseType,Server,ServerVersion,Port,IntegratedSecurity,UserId,Password,IsDba,Usessl 
                                FROM Account
                                WHERE DatabaseType=@DbType AND Server=@Server");

                    var para = new Dictionary<string, object>
                    {
                        { "@DbType", dbType },
                        { "@Server", server }
                    };

                    if (string.IsNullOrEmpty(port))
                    {
                        sb.Append("AND Port IS NULL");
                    }
                    else
                    {
                        sb.Append("AND Port=@Port");

                        para.Add("@Port", port);
                    }

                    if (integratedSecurity)
                    {
                        sb.Append($"AND IntegratedSecurity={ValueHelper.BooleanToInteger(integratedSecurity)}");
                    }
                    else if (userId != null)
                    {
                        sb.Append("AND UserId=@UserId");

                        para.Add("@UserId", userId);
                    }

                    profile = (await connection.QueryAsync<AccountProfileInfo>(sb.Content, para)).FirstOrDefault();

                    if (profile != null && !string.IsNullOrEmpty(profile.Password))
                        profile.Password = AesHelper.Decrypt(profile.Password);
                }

            return profile;
        }

        public static async Task<AccountProfileInfo> GetProfile(string id)
        {
            AccountProfileInfo profile = null;

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sb = new SqlBuilder();

                    sb.Append(
                        @"SELECT Id,DatabaseType,Server,ServerVersion,Port,IntegratedSecurity,UserId,Password,IsDba,Usessl 
                                FROM Account
                                WHERE Id=@Id");

                    var para = new Dictionary<string, object>
                    {
                        { "@Id", id }
                    };

                    profile = (await connection.QueryAsync<AccountProfileInfo>(sb.Content, para)).FirstOrDefault();

                    if (profile != null && !string.IsNullOrEmpty(profile.Password))
                        profile.Password = AesHelper.Decrypt(profile.Password);
                }

            return profile;
        }

        public static async Task<string> Save(AccountProfileInfo info, bool rememberPassword)
        {
            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var trans = connection.BeginTransaction();

                    var result = await Save(info, rememberPassword, connection);

                    if (result.AffectRowsCount > 0)
                        if (trans != null)
                            trans.Commit();

                    return result.Id;
                }

            return string.Empty;
        }

        internal static async Task<SaveResultInfo> Save(AccountProfileInfo info, bool rememberPassword,
            SqliteConnection connection)
        {
            var profiles = await GetProfiles(info.DatabaseType);

            var oldProfile = profiles.FirstOrDefault(item => item.Id == info.Id);

            var id = info.Id;

            var result = -1;

            if (oldProfile == null)
            {
                var password = info.Password;

                if (!rememberPassword || info.IntegratedSecurity)
                    password = null;
                else if (rememberPassword && !string.IsNullOrEmpty(info.Password))
                    password = AesHelper.Encrypt(info.Password);

                id = Guid.NewGuid().ToString();

                var sql =
                    $@"INSERT INTO Account(Id,DatabaseType,Server,ServerVersion,Port,IntegratedSecurity,UserId,Password,IsDba,UseSsl)
                                        VALUES('{id}',@DbType,@Server,@ServerVersion,@Port,{ValueHelper.BooleanToInteger(info.IntegratedSecurity)},@UserId,@Password,{ValueHelper.BooleanToInteger(info.IsDba)},{ValueHelper.BooleanToInteger(info.UseSsl)})";

                var cmd = connection.CreateCommand();
                cmd.CommandText = sql;

                cmd.Parameters.AddWithValue("@DbType", info.DatabaseType);
                cmd.Parameters.AddWithValue("@Server", info.Server);
                cmd.Parameters.AddWithValue("@ServerVersion", GetParameterValue(info.ServerVersion));
                cmd.Parameters.AddWithValue("@Port", GetParameterValue(info.Port));
                cmd.Parameters.AddWithValue("@UserId", GetParameterValue(info.UserId));
                cmd.Parameters.AddWithValue("@Password", GetParameterValue(password));

                result = await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                id = oldProfile.Id;

                var password = oldProfile.Password;

                if (rememberPassword)
                {
                    if (!string.IsNullOrEmpty(info.Password)) password = AesHelper.Encrypt(info.Password);
                }
                else
                {
                    password = null;
                }

                var sql = $@"UPDATE Account SET 
                                        Server=@Server,
                                        ServerVersion=@ServerVersion,
                                        Port=@Port,
                                        IntegratedSecurity={info.IntegratedSecurity},
                                        UserId=@UserId,
                                        Password = @Password,
                                        IsDba={ValueHelper.BooleanToInteger(info.IsDba)},
                                        UseSsl = {ValueHelper.BooleanToInteger(info.UseSsl)}
                                        WHERE Id=@Id";

                var cmd = connection.CreateCommand();
                cmd.CommandText = sql;

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Server", info.Server);
                cmd.Parameters.AddWithValue("@ServerVersion", GetParameterValue(info.ServerVersion));
                cmd.Parameters.AddWithValue("@Port", GetParameterValue(info.Port));
                cmd.Parameters.AddWithValue("@UserId", GetParameterValue(info.UserId));
                cmd.Parameters.AddWithValue("@Password", GetParameterValue(password));

                result = await cmd.ExecuteNonQueryAsync();
            }

            return new SaveResultInfo { AffectRowsCount = result, Id = id };
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

                    var sql = $@"DELETE FROM Account
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

    internal class SaveResultInfo
    {
        internal int AffectRowsCount { get; set; }
        internal string Id { get; set; }
    }
}