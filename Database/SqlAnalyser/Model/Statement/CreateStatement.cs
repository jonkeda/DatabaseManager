using DatabaseInterpreter.Model;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public class CreateStatement : Statement
    {
        public virtual DatabaseObjectType ObjectType { get; set; }
        public NameToken ObjectName { get; set; }
    }
}