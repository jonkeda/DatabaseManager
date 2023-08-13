namespace Databases.Model.DatabaseObject
{
    public class Table : DatabaseObject
    {
        public string Definition { get; set; }
        public string Comment { get; set; }
        public int? IdentitySeed { get; set; }
        public int? IdentityIncrement { get; set; }
    }
}