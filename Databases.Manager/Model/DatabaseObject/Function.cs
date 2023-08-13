using Databases.Model.DatabaseObject.Fiction;

namespace Databases.Model.DatabaseObject
{
    public class Function : ScriptDbObject
    {
        public string DataType { get; set; }
        public bool IsTriggerFunction { get; set; }
    }
}