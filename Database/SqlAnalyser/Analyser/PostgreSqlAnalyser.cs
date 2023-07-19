using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class PostgreSqlAnalyser : SqlAnalyserBase
    {
        private readonly PostgreSqlRuleAnalyser ruleAnalyser;

        public PostgreSqlAnalyser(string content) : base(content)
        {
            ruleAnalyser = new PostgreSqlRuleAnalyser(content);
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