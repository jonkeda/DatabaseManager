namespace DatabaseInterpreter.Model
{
    public class DatabaseObjectScript<T> : Script
        where T : DatabaseObject
    {
        public DatabaseObjectScript(string script) : base(script)
        {
            ObjectType = typeof(T).Name;
        }
    }
}