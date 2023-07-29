using System;
using DatabaseInterpreter.Model;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public abstract class SqlAnalyserBase
    {
        protected SqlAnalyserBase(string content)
        {
            Content = content;
        }

        public string Content { get; set; }
        public abstract SqlRuleAnalyser RuleAnalyser { get; }

        public abstract SqlSyntaxError Validate();
        public abstract AnalyseResult AnalyseCommon();
        public abstract AnalyseResult AnalyseView();
        public abstract AnalyseResult AnalyseProcedure();
        public abstract AnalyseResult AnalyseFunction();
        public abstract AnalyseResult AnalyseTrigger();

        public AnalyseResult Analyse<T>() where T : DatabaseObject
        {
            AnalyseResult result;

            if (RuleAnalyser.Option.IsCommonScript)
            {
                result = AnalyseCommon();
            }
            else
            {
                if (typeof(T) == typeof(Procedure))
                {
                    result = AnalyseProcedure();
                }
                else if (typeof(T) == typeof(Function))
                {
                    result = AnalyseFunction();
                }
                else if (typeof(T) == typeof(View))
                {
                    result = AnalyseView();
                }
                else if (typeof(T) == typeof(TableTrigger))
                {
                    result = AnalyseTrigger();
                }
                else
                {
                    throw new NotSupportedException($"Not support analyse for type:{typeof(T).Name}");
                }
            }

            return result;
        }
    }
}