namespace Databases.Model.Script
{
    public class DropDbObjectScript<T> : DatabaseObjectScript<T>
        where T : DatabaseObject.DatabaseObject
    {
        public DropDbObjectScript(string script) : base(script)
        { }
    }
}