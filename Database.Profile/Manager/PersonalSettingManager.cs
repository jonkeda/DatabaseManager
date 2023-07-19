using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseInterpreter.Utility;

namespace DatabaseManager.Profile
{
    public class PersonalSettingManager : ProfileBaseManager
    {
        public static async Task<PersonalSetting> GetPersonalSetting()
        {
            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var sql = "SELECT Id, LockPassword FROM PersonalSetting WHERE Id=1";

                    var setting = (await connection.QueryAsync<PersonalSetting>(sql))?.FirstOrDefault();

                    if (setting != null && !string.IsNullOrEmpty(setting.LockPassword))
                        setting.LockPassword = AesHelper.Decrypt(setting.LockPassword);

                    return setting;
                }

            return null;
        }

        public static async Task<bool> Save(PersonalSetting setting)
        {
            if (ExistsProfileDataFile())
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var trans = connection.BeginTransaction();

                    var sql = "UPDATE PersonalSetting SET LockPassword=@LockPassword WHERE Id=1";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;

                    var lockPassword = string.IsNullOrEmpty(setting.LockPassword)
                        ? null
                        : AesHelper.Encrypt(setting.LockPassword);

                    cmd.Parameters.AddWithValue("@LockPassword", GetParameterValue(lockPassword));

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