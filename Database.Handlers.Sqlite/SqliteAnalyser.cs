using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model;

namespace Databases.Handlers.Sqlite
{
    public class SqliteAnalyser : SqlAnalyserBase
    {
        private readonly SqliteRuleAnalyser ruleAnalyser;

        public SqliteAnalyser(string content) : base(content)
        {
            ruleAnalyser = new SqliteRuleAnalyser(content);
        }

        public override SqlRuleAnalyser RuleAnalyser => ruleAnalyser;

        public override SqlSyntaxError Validate()
        {
            return ruleAnalyser.Validate();
        }

        public override AnalyseResult AnalyseCommon()
        {
            return ruleAnalyser.AnalyseCommon();
        }

        public override AnalyseResult AnalyseView()
        {
            return ruleAnalyser.AnalyseView();
        }

        public override AnalyseResult AnalyseProcedure()
        {
            return ruleAnalyser.AnalyseProcedure();
        }

        public override AnalyseResult AnalyseFunction()
        {
            return ruleAnalyser.AnalyseFunction();
        }

        public override AnalyseResult AnalyseTrigger()
        {
            return ruleAnalyser.AnalyseTrigger();
        }
    }
}