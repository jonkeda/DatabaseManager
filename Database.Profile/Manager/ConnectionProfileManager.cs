﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseManager.Model;
using Databases.Interpreter.Builder;
using Databases.Interpreter.Utility.Helper;
using Databases.Model.Connection;

namespace DatabaseManager.Profile
{
    public class ConnectionProfileManager : ProfileBaseManager
    {
        public static async Task<IEnumerable<ConnectionProfileInfo>> GetProfiles(string databaseType)
        {
            return await GetProfiles(new ProfileFilter { DatabaseType = databaseType });
        }

        public static async Task<IEnumerable<ConnectionProfileInfo>> GetProfilesByAccountId(string accountId)
        {
            return await GetProfiles(new ProfileFilter { AccountId = accountId });
        }

        public static async Task<ConnectionProfileInfo> GetProfileById(string id)
        {
            return (await GetProfiles(new ProfileFilter { Id = id }))?.FirstOrDefault();
        }

        private static async Task<IEnumerable<ConnectionProfileInfo>> GetProfiles(ProfileFilter filter)
        {
            var profiles = Enumerable.Empty<ConnectionProfileInfo>();

            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sb = new SqlBuilder();

                    sb.Append(
                        @"SELECT c.Id,c.AccountId, DatabaseType,Server,ServerVersion,Port,IntegratedSecurity,UserId,Password,IsDba,UseSsl,
                                c.Name,c.Database,c.Visible
                                FROM Connection c 
                                JOIN Account a ON A.Id=c.AccountId
                                WHERE 1=1");


                    var dbType = filter?.DatabaseType;
                    var accountId = filter?.AccountId;
                    var id = filter?.Id;
                    var name = filter?.Name;

                    var para = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(dbType))
                    {
                        sb.Append("AND a.DatabaseType=@DbType");

                        para.Add("@DbType", filter.DatabaseType);
                    }

                    if (!string.IsNullOrEmpty(accountId))
                    {
                        sb.Append("AND AccountId=@AccountId");

                        para.Add("@AccountId", accountId);
                    }

                    if (!string.IsNullOrEmpty(id))
                    {
                        sb.Append("AND c.Id=@Id");

                        para.Add("@Id", id);
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        sb.Append("AND c.Name=@Name");

                        para.Add("@Name", name);
                    }

                    profiles = await connection.QueryAsync<ConnectionProfileInfo>(sb.Content, para);

                    if (profiles != null)
                        foreach (var profile in profiles)
                            if (!string.IsNullOrEmpty(profile.Password))
                                profile.Password = AesHelper.Decrypt(profile.Password);
                }

            return profiles;
        }

        public static async Task<ConnectionInfo> GetConnectionInfo(string dbType, string name)
        {
            var profile =
                (await GetProfiles(new ProfileFilter { DatabaseType = dbType, Name = name }))?.FirstOrDefault();

            if (profile != null)
            {
                var connectionInfo = new ConnectionInfo
                {
                    Server = profile.Server,
                    IntegratedSecurity = profile.IntegratedSecurity,
                    Port = profile.Port,
                    UserId = profile.UserId,
                    Password = profile.Password,
                    IsDba = profile.IsDba,
                    UseSsl = profile.UseSsl,
                    Database = profile.Database,
                    ServerVersion = profile.ServerVersion
                };

                return connectionInfo;
            }

            return null;
        }

        public static async Task<string> Save(ConnectionProfileInfo info, bool rememberPassword)
        {
            var password = info.Password;

            if (info.IntegratedSecurity || !rememberPassword) password = null;

            var profileName = info.Name;

            if (string.IsNullOrEmpty(profileName)) profileName = info.ConnectionDescription;

            AccountProfileInfo accountProfile = null;

            if (!string.IsNullOrEmpty(info.AccountId))
                accountProfile = await AccountProfileManager.GetProfile(info.AccountId);

            var changed = false;

            if (accountProfile != null)
            {
                if ((!accountProfile.IntegratedSecurity && accountProfile.Password != password)
                    || accountProfile.ServerVersion != info.ServerVersion)
                {
                    changed = true;

                    accountProfile.Password = password;

                    if (!string.IsNullOrEmpty(info.ServerVersion)) accountProfile.ServerVersion = info.ServerVersion;
                }
            }
            else
            {
                changed = true;

                accountProfile = new AccountProfileInfo
                {
                    DatabaseType = info.DatabaseType,
                    Server = info.Server,
                    Port = info.Port,
                    IntegratedSecurity = info.IntegratedSecurity,
                    ServerVersion = info.ServerVersion,
                    UserId = info.UserId,
                    Password = password,
                    IsDba = info.IsDba,
                    UseSsl = info.UseSsl
                };
            }

            var profiles = Enumerable.Empty<ConnectionProfileInfo>();

            if (ExistsProfileDataFile()) profiles = await GetProfiles(info.DatabaseType);

            var oldProfile =
                profiles.FirstOrDefault(item => item.Name == info.Name && item.DatabaseType == info.DatabaseType);

            using (var connection = CreateDbConnection())
            {
                await connection.OpenAsync();

                var trans = connection.BeginTransaction();

                if (changed)
                {
                    var resultInfo = await AccountProfileManager.Save(accountProfile, rememberPassword, connection);

                    if (accountProfile.Id == null && resultInfo.Id != null)
                        info.AccountId = accountProfile.Id = resultInfo.Id;
                }

                if (oldProfile == null)
                {
                    var id = Guid.NewGuid().ToString();

                    var sql = @"INSERT INTO Connection(Id,AccountId,Name,Database)
                                    VALUES(@Id,@AccountId,@Name,@Database)";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;

                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@AccountId", accountProfile.Id);
                    cmd.Parameters.AddWithValue("@Name", info.Name);
                    cmd.Parameters.AddWithValue("@Database", info.Database);

                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var sql = @"UPDATE Connection
                                    SET Name=@Name,Database=@Database
                                    WHERE ID=@Id";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;

                    cmd.Parameters.AddWithValue("@Id", oldProfile.Id);
                    cmd.Parameters.AddWithValue("@Name", info.Name);
                    cmd.Parameters.AddWithValue("@Database", info.Database);

                    await cmd.ExecuteNonQueryAsync();
                }

                trans.Commit();
            }

            return profileName;
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

                    var sql = $@"DELETE FROM Connection
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