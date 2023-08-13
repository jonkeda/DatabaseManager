namespace Databases.Model.Script
{
    public class CreateDbObjectScript<T> : DatabaseObjectScript<T>
        where T : DatabaseObject.DatabaseObject
    {
        public CreateDbObjectScript(string script) : base(script)
        { }
    }
}