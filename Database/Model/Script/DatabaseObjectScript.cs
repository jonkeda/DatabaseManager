﻿namespace Databases.Model.Script
{
    public class DatabaseObjectScript<T> : Script
        where T : DatabaseObject.DatabaseObject
    {
        public DatabaseObjectScript(string script) : base(script)
        {
            ObjectType = typeof(T).Name;
        }
    }
}