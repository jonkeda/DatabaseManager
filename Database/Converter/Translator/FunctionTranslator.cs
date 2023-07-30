using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Token;

namespace DatabaseConverter.Core
{
    public class FunctionTranslator : DbObjectTranslator
    {
        private readonly IEnumerable<TokenInfo> functions;
        private List<FunctionSpecification> sourceFuncSpecs;
        private List<FunctionSpecification> targetFuncSpecs;

        public FunctionTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter) : base(
            sourceInterpreter, targetInterpreter)
        { }

        public FunctionTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            IEnumerable<TokenInfo> functions) : base(sourceInterpreter, targetInterpreter)
        {
            this.functions = functions;
        }

        public RoutineType RoutineType { get; set; }

        public override void Translate()
        {
            if (sourceDbType == targetDbType)
            {
                return;
            }

            LoadMappings();
            LoadFunctionSpecifications();

            if (functions != null)
            {
                foreach (var token in functions)
                {
                    token.Symbol = GetMappedFunction(token.Symbol);
                }
            }
        }

        public void LoadFunctionSpecifications()
        {
            sourceFuncSpecs = FunctionManager.GetFunctionSpecifications(sourceDbType);
            targetFuncSpecs = FunctionManager.GetFunctionSpecifications(targetDbType);
        }

        public string GetMappedFunction(string value)
        {
            if (sourceDbType == DatabaseType.Postgres)
            {
                value = value.ReplaceOrdinalIgnoreCase(@"""substring""", "substring");
            }

            var formulas = GetFunctionFormulas(sourceDbInterpreter, value);

            foreach (var formula in formulas)
            {
                var name = formula.Name;

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var sourceFuncSpec = sourceFuncSpecs.FirstOrDefault(item => item.Name == name.ToUpper());

                if (sourceFuncSpec == null)
                {
                    continue;
                }

                var targetFunctionInfo = GetMappingFunctionInfo(name, formula.Body, out var useBrackets);

                if (!string.IsNullOrEmpty(targetFunctionInfo.Name))
                {
                    if (targetFunctionInfo.Name.ToUpper().Trim() != name.ToUpper().Trim())
                    {
                        var noParenthesess = false;
                        var hasArgs = false;

                        var targetFuncSpec =
                            targetFuncSpecs.FirstOrDefault(item => item.Name == targetFunctionInfo.Name);

                        if (targetFuncSpec != null)
                        {
                            #region Handle specials

                            if (!string.IsNullOrEmpty(targetFunctionInfo.Specials))
                            {
                                var sourceArgItems = GetFunctionArgumentTokens(sourceFuncSpec, null);
                                var targetArgItems = GetFunctionArgumentTokens(targetFuncSpec, targetFunctionInfo.Args);

                                var args = formula.GetArgs();

                                Func<string, string> getTrimedContent = content => { return content.Trim('\''); };

                                foreach (var tai in targetArgItems)
                                {
                                    var upperContent = tai.Content.ToUpper();

                                    if (upperContent == "UNIT" || upperContent == "'UNIT'")
                                    {
                                        var sourceItem = sourceArgItems.FirstOrDefault(item =>
                                            getTrimedContent(item.Content) == getTrimedContent(tai.Content));

                                        if (sourceItem != null && args.Count > sourceItem.Index)
                                        {
                                            var arg = args[sourceItem.Index];

                                            var specials = targetFunctionInfo.Specials.Split(';');

                                            foreach (var special in specials)
                                            {
                                                var items = special.Split(':');

                                                var subItems = items[0].Split('=');

                                                var k = subItems[0];
                                                var v = subItems[1];

                                                if (k.ToUpper() == upperContent)
                                                {
                                                    var mappedUnit =
                                                        DatetimeHelper.GetMappedUnit(sourceDbType, targetDbType, arg);

                                                    if (mappedUnit == v)
                                                    {
                                                        targetFunctionInfo = new MappingFunctionInfo
                                                            { Name = items[1] };

                                                        targetFuncSpec = targetFuncSpecs.FirstOrDefault(item =>
                                                            item.Name == targetFunctionInfo.Name);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            #endregion

                            noParenthesess = targetFuncSpec.NoParenthesess;

                            hasArgs = !string.IsNullOrEmpty(targetFuncSpec.Args);
                        }

                        var oldExp = formula.Expression;
                        //string newExp = ReplaceValue(formula.Expression, name, targetFunctionInfo.Name);
                        var newExp =
                            $"{targetFunctionInfo.Name}{(formula.HasParentheses ? "(" : "")}{formula.Body}{(formula.HasParentheses ? ")" : "")}";

                        if (!hasArgs && !string.IsNullOrEmpty(formula.Body))
                        {
                            newExp = $"{targetFunctionInfo.Name}()";
                        }

                        if (noParenthesess)
                        {
                            newExp = newExp.Replace("()", "");
                        }
                        else
                        {
                            if (sourceFuncSpec.NoParenthesess && targetFuncSpec != null &&
                                string.IsNullOrEmpty(targetFuncSpec.Args))
                            {
                                newExp += "()";
                            }
                        }

                        newExp = newExp.Replace("()()", "()");

                        formula.Expression = newExp;

                        value = ReplaceValue(value, oldExp, newExp);
                    }
                }

                var newExpression = ParseFormula(sourceFuncSpecs, targetFuncSpecs, formula, targetFunctionInfo,
                    out var dictDataType, RoutineType);

                if (newExpression != formula.Expression)
                {
                    value = ReplaceValue(value, formula.Expression, newExpression);
                }
            }

            return value;
        }

        public static List<FunctionFormula> GetFunctionFormulas(DbInterpreter dbInterpreter, string value,
            bool extractChildren = true)
        {
            value = StringHelper.GetBalanceParenthesisTrimedValue(value);

            var functionSpecifications = FunctionManager.GetFunctionSpecifications(dbInterpreter.DatabaseType);

            var functions = new List<FunctionFormula>();

            var trimChars = TranslateHelper.GetTrimChars(dbInterpreter).ToArray();

            bool IsValidFunction(string name)
            {
                return functionSpecifications.Any(item => item.Name.ToUpper() == name.Trim().Trim(trimChars).ToUpper());
            }

            if (value.IndexOf("(", StringComparison.Ordinal) < 0)
            {
                if (IsValidFunction(value))
                {
                    functions.Add(new FunctionFormula(value, value));
                }
            }
            else
            {
                var select = "SELECT ";

                var sql = $"{select}{value}";

                if (dbInterpreter.DatabaseType == DatabaseType.Oracle)
                {
                    sql += " FROM DUAL";
                }

                var sqlAnalyser = TranslateHelper.GetSqlAnalyser(dbInterpreter.DatabaseType, sql);

                sqlAnalyser.RuleAnalyser.Option.ParseTokenChildren = false;
                sqlAnalyser.RuleAnalyser.Option.ExtractFunctions = true;
                sqlAnalyser.RuleAnalyser.Option.ExtractFunctionChildren = extractChildren;
                sqlAnalyser.RuleAnalyser.Option.IsCommonScript = true;

                var result = sqlAnalyser.AnalyseCommon();

                if (!result.HasError)
                {
                    var tokens = result.Script.Functions;

                    foreach (var token in tokens)
                    {
                        var symbol = token.Symbol;

                        var name = TranslateHelper.ExtractNameFromParenthesis(symbol);

                        if (IsValidFunction(name))
                        {
                            TranslateHelper.RestoreTokenValue(sql, token);

                            var formula = new FunctionFormula(name, token.Symbol);

                            functions.Add(formula);
                        }
                    }
                }
            }

            return functions;
        }
    }
}