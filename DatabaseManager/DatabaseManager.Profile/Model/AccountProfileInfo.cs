using DatabaseInterpreter.Model;

namespace DatabaseManager.Profile
{
    public class AccountProfileInfo : DatabaseAccountInfo
    {
        public string Id { get; set; }

        public string DatabaseType { get; set; }

        public string Description =>
            $"{(!string.IsNullOrEmpty(UserId) ? UserId : "Integrated Security")}({Server}{(string.IsNullOrEmpty(Port) ? "" : ":" + Port)})";
    }
}