namespace Databases.Model.Script
{
    public class Script
    {
        public Script()
        { }

        public Script(string script)
        {
            Content = script;
        }

        public string ObjectType { get; protected set; }
        public string Content { get; set; }
    }
}