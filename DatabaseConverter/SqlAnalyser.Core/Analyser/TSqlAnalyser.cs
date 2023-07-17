using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class TSqlAnalyser : SqlAnalyserBase
    {
        private readonly TSqlRuleAnalyser ruleAnalyser;

        public TSqlAnalyser(string content) : base(content)
        {
            ruleAnalyser = new TSqlRuleAnalyser(content);
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