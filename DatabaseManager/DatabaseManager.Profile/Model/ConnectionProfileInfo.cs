using DatabaseInterpreter.Model;

namespace DatabaseManager.Profile
{
    public class ConnectionProfileInfo : ConnectionInfo
    {
        public string Id { get; set; }
        public string AccountId { get; set; }
        public string Name { get; set; }
        public string DatabaseType { get; set; }
        public bool Visible { get; set; } = true;

        public string ConnectionDescription =>
            $"server={Server}{(string.IsNullOrEmpty(Port) ? "" : ":" + Port)};database={Database}";

        public string Description
        {
            get
            {
                var connectionDescription = ConnectionDescription;

                if (Name == connectionDescription)
                    return Name;
                return $"{Name}({connectionDescription})";
            }
        }
    }
}