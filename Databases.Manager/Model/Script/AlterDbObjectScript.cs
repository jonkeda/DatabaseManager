namespace Databases.Model.Script
{
    public class AlterDbObjectScript<T> : DatabaseObjectScript<T>
        where T : DatabaseObject.DatabaseObject
    {
        public AlterDbObjectScript(string script) : base(script)
        { }
    }
}