using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class ScriptCorrector
    {
        private readonly DbInterpreter dbInterpreter;
        private readonly char quotationLeftChar;
        private readonly char quotationRightChar;

        public ScriptCorrector(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
            quotationLeftChar = dbInterpreter.QuotationLeftChar;
            quotationRightChar = dbInterpreter.QuotationRightChar;
        }

        public async Task<IEnumerable<ScriptDiagnoseResult>> CorrectNotMatchNames(ScriptDiagnoseType scriptDiagnoseType,
            IEnumerable<ScriptDiagnoseResult> results)
        {
            var scripts = new List<Script>();

            var dictDifinition = new Dictionary<int, string>();

            var i = 0;

            foreach (var result in results)
            {
                var scriptGenerator = new ScriptGenerator(dbInterpreter);

                var script = (await scriptGenerator.Generate(result.DbObject, ScriptAction.ALTER)).Script;

                if (string.IsNullOrEmpty(script))
                {
                    continue;
                }

                var definition = result.DbObject.Definition;

                foreach (var detail in result.Details)
                {
                    script = ReplaceDefinition(script, detail.InvalidName, detail.Name);
                    definition = ReplaceDefinition(definition, detail.InvalidName, detail.Name);

                    if (scriptDiagnoseType == ScriptDiagnoseType.ViewColumnAliasWithoutQuotationChar)
                    {
                        script = ReplaceDuplicateQuotationChar(script, detail.Name);
                        definition = ReplaceDuplicateQuotationChar(definition, detail.Name);
                    }
                }

                if (result.DbObject is View)
                {
                    scripts.Add(new AlterDbObjectScript<View>(script));
                }
                else if (result.DbObject is Procedure)
                {
                    scripts.Add(new AlterDbObjectScript<Procedure>(script));
                }
                else if (result.DbObject is Function)
                {
                    scripts.Add(new AlterDbObjectScript<Function>(script));
                }

                dictDifinition.Add(i, definition);

                i++;
            }

            var scriptRunner = new ScriptRunner();

            await scriptRunner.Run(dbInterpreter, scripts);

            i = 0;

            foreach (var result in results)
            {
                result.DbObject.Definition = dictDifinition[i];

                i++;
            }

            return results;
        }

        private string ReplaceDuplicateQuotationChar(string content, string value)
        {
            content = content.Replace($"{quotationLeftChar}{value}{quotationRightChar}", value);

            return content;
        }

        private string ReplaceDefinition(string definition, string oldValue, string newValue)
        {
            return Regex.Replace(definition, $@"\b{oldValue}\b", newValue, RegexOptions.Multiline);
        }
    }
}