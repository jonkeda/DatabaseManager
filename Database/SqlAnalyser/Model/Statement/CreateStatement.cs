using Databases.Model.Schema;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.SqlAnalyser.Model.Statement
{
    public abstract class CreateStatement : Statement
    {
        public virtual DatabaseObjectType ObjectType { get; set; }
        public NameToken ObjectName { get; set; }
    }
}