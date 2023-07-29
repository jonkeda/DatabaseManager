using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseConverter.Core.Functions;
using DatabaseConverter.Core.Model;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using PoorMansTSqlFormatterRedux;
using SqlAnalyser.Model;

namespace DatabaseConverter.Core
{
    public abstract class DbObjectTranslator : IDisposable
    {
        protected List<DataTypeMapping> dataTypeMappings;
        protected List<IEnumerable<FunctionMapping>> functionMappings;
        protected bool hasError = false;
        private IObserver<FeedbackInfo> observer;
        protected DbInterpreter sourceDbInterpreter;
        protected DatabaseType sourceDbType;
        protected string sourceSchemaName;
        protected DbInterpreter targetDbInterpreter;
        protected DatabaseType targetDbType;
        protected List<IEnumerable<VariableMapping>> variableMappings;

        public DbObjectTranslator(DbInterpreter source, DbInterpreter target)
        {
            sourceDbInterpreter = source;
            targetDbInterpreter = target;
            sourceDbType = source.DatabaseType;
            targetDbType = target.DatabaseType;
        }

        public bool ContinueWhenErrorOccurs { get; set; }
        public bool HasError => hasError;
        public SchemaInfo SourceSchemaInfo { get; set; }
        public DbConverterOption Option { get; set; }
        public List<UserDefinedType> UserDefinedTypes { get; set; } = new List<UserDefinedType>();

        public List<TranslateResult> TranslateResults { get; internal set; } = new List<TranslateResult>();

        public void Dispose()
        {
            TranslateResults.Clear();
        }

        public void LoadMappings()
        {
            if (sourceDbInterpreter.DatabaseType != targetDbInterpreter.DatabaseType)
            {
                functionMappings = FunctionMappingManager.FunctionMappings;
                variableMappings = VariableMappingManager.VariableMappings;
                dataTypeMappings = DataTypeMappingManager.GetDataTypeMappings(sourceDbInterpreter.DatabaseType,
                    targetDbInterpreter.DatabaseType);
            }
        }

        public abstract void Translate();

        public DataTypeMapping GetDataTypeMapping(List<DataTypeMapping> mappings, string dataType)
        {
            return mappings.FirstOrDefault(item => item.Source.Type?.ToLower() == dataType?.ToLower());
        }

        internal string GetNewDataType(List<DataTypeMapping> mappings, string dataType, bool usedForFunction = true)
        {
            dataType = GetTrimedValue(dataType.Trim());
            var dataTypeInfo = sourceDbInterpreter.GetDataTypeInfo(dataType);

            var upperDataTypeName = dataTypeInfo.DataType.ToUpper();

            if (sourceDbType == DatabaseType.MySql)
            {
                if (upperDataTypeName == "SIGNED")
                {
                    if (targetDbType == DatabaseType.SqlServer)
                    {
                        return "DECIMAL";
                    }

                    if (targetDbType == DatabaseType.Postgres)
                    {
                        return "NUMERIC";
                    }

                    if (targetDbType == DatabaseType.Oracle)
                    {
                        return "NUMBER";
                    }
                }
            }

            if (targetDbType == DatabaseType.Oracle)
            {
                if (usedForFunction && GetType() == typeof(FunctionTranslator) && dataTypeInfo.Args?.ToLower() == "max")
                {
                    var mappedDataType = GetDataTypeMapping(mappings, dataTypeInfo.DataType)?.Target?.Type;

                    var isChar = DataTypeHelper.IsCharType(mappedDataType);
                    var isBinary = DataTypeHelper.IsBinaryType(mappedDataType);

                    if (isChar || isBinary)
                    {
                        var dataTypeSpec = targetDbInterpreter.GetDataTypeSpecification(mappedDataType);

                        if (dataTypeSpec != null)
                        {
                            var range = DataTypeManager.GetArgumentRange(dataTypeSpec, "length");

                            if (range.HasValue)
                            {
                                return $"{mappedDataType}({range.Value.Max})";
                            }
                        }
                    }
                }
            }

            var trimChars = TranslateHelper.GetTrimChars(sourceDbInterpreter, targetDbInterpreter).ToArray();

            var column =
                TranslateHelper.SimulateTableColumn(sourceDbInterpreter, dataType, Option, UserDefinedTypes, trimChars);

            var dataTypeTranslator = new DataTypeTranslator(sourceDbInterpreter, targetDbInterpreter);
            dataTypeTranslator.Option = Option;

            TranslateHelper.TranslateTableColumnDataType(dataTypeTranslator, column);

            var newDataTypeName = column.DataType;
            string newDataType = null;

            if (usedForFunction)
            {
                if (targetDbType == DatabaseType.MySql)
                {
                    var ndt = GetMySqlNewDataType(newDataTypeName);

                    if (ndt != newDataTypeName)
                    {
                        newDataType = ndt;
                    }
                }
                else if (targetDbType == DatabaseType.Postgres)
                {
                    if (DataTypeHelper.IsBinaryType(newDataTypeName))
                    {
                        return newDataTypeName;
                    }
                }
            }

            if (string.IsNullOrEmpty(newDataType))
            {
                newDataType = targetDbInterpreter.ParseDataType(column);
            }

            return newDataType;
        }

        private string GetMySqlNewDataType(string dataTypeName)
        {
            var upperTypeName = dataTypeName.ToUpper();

            if (upperTypeName.Contains("INT") || upperTypeName == "BIT")
            {
                return "SIGNED";
            }

            if (upperTypeName == "NUMBER")
            {
                return "DOUBLE";
            }

            if (DataTypeHelper.IsCharType(dataTypeName) || upperTypeName.Contains("TEXT"))
            {
                return "CHAR";
            }

            if (DataTypeHelper.IsDateOrTimeType(dataTypeName))
            {
                return "DATETIME";
            }

            if (DataTypeHelper.IsBinaryType(dataTypeName))
            {
                return "BINARY";
            }

            return dataTypeName;
        }

        private string GetTrimedValue(string value)
        {
            return value?.Trim(sourceDbInterpreter.QuotationLeftChar, sourceDbInterpreter.QuotationRightChar);
        }

        public static string ReplaceValue(string source, string oldValue, string newValue,
            RegexOptions option = RegexOptions.IgnoreCase)
        {
            return RegexHelper.Replace(source, oldValue, newValue, option);
        }

        public string ExchangeFunctionArgs(string functionName, string args1, string args2)
        {
            if (functionName.ToUpper() == "CONVERT" && targetDbInterpreter.DatabaseType == DatabaseType.MySql &&
                args1.ToUpper().Contains("DATE"))
            {
                if (args2.Contains(','))
                {
                    args2 = args2.Split(',')[0];
                }
            }

            var newExpression = $"{functionName}({args2},{args1})";

            return newExpression;
        }

        public string ReplaceVariables(string script, List<IEnumerable<VariableMapping>> mappings)
        {
            if (mappings == null)
            {
                return script;
            }

            foreach (var mapping in mappings)
            {
                var sourceVariable =
                    mapping.FirstOrDefault(item => item.DbType == sourceDbInterpreter.DatabaseType.ToString());
                var targetVariable =
                    mapping.FirstOrDefault(item => item.DbType == targetDbInterpreter.DatabaseType.ToString());

                if (sourceVariable != null && !string.IsNullOrEmpty(sourceVariable.Variable)
                                           && targetVariable != null && targetVariable.Variable != null &&
                                           !string.IsNullOrEmpty(targetVariable.Variable)
                   )
                {
                    script = ReplaceValue(script, sourceVariable.Variable, targetVariable.Variable);
                }
            }

            return script;
        }

        public string ParseFormula(List<FunctionSpecification> sourceFuncSpecs,
            List<FunctionSpecification> targetFuncSpecs,
            FunctionFormula formula, MappingFunctionInfo targetFunctionInfo,
            out Dictionary<string, string> dictDataType, RoutineType routineType = RoutineType.UNKNOWN)
        {
            dictDataType = new Dictionary<string, string>();

            var name = formula.Name;

            if (!string.IsNullOrEmpty(targetFunctionInfo.Args) && targetFunctionInfo.IsFixedArgs)
            {
                return $"{targetFunctionInfo.Name}({targetFunctionInfo.Args})";
            }

            var sourceFuncSpec = sourceFuncSpecs.FirstOrDefault(item => item.Name.ToUpper() == name.ToUpper());
            var targetFuncSpec =
                targetFuncSpecs.FirstOrDefault(item => item.Name.ToUpper() == targetFunctionInfo.Name.ToUpper());

            var newExpression = formula.Expression;

            if (sourceFuncSpec != null && !string.IsNullOrEmpty(targetFunctionInfo.Translator))
            {
                var type = Type.GetType(
                    $"{typeof(SpecificFunctionTranslatorBase).Namespace}.{targetFunctionInfo.Translator}");

                var translator =
                    (SpecificFunctionTranslatorBase)Activator.CreateInstance(type, sourceFuncSpec, targetFuncSpec);

                translator.SourceDbType = sourceDbType;
                translator.TargetDbType = targetDbType;

                newExpression = translator.Translate(formula);

                return newExpression;
            }

            var dataTypeDict = new Dictionary<string, string>();

            if (sourceFuncSpec != null)
            {
                var delimiter = sourceFuncSpec.Delimiter.Length == 1
                    ? sourceFuncSpec.Delimiter
                    : $" {sourceFuncSpec.Delimiter} ";

                var formulaArgs = formula.GetArgs(delimiter);

                var sourceArgItems = GetFunctionArgumentTokens(sourceFuncSpec, null);
                var targetArgItems = targetFuncSpec == null
                    ? null
                    : GetFunctionArgumentTokens(targetFuncSpec, targetFunctionInfo.Args);

                var ignore = false;

                if (targetArgItems == null || (formulaArgs.Count > 0 &&
                                               (targetArgItems == null || targetArgItems.Count == 0 ||
                                                sourceArgItems.Count == 0)))
                {
                    ignore = true;
                }

                Func<FunctionArgumentItemInfo, string, string> getSourceArg = (source, content) =>
                {
                    var sourceIndex = source.Index;

                    if (formulaArgs.Count > sourceIndex)
                    {
                        var oldArg = formulaArgs[sourceIndex];
                        var newArg = oldArg;

                        switch (content.ToUpper())
                        {
                            case "TYPE":

                                if (!dataTypeDict.ContainsKey(oldArg))
                                {
                                    newArg = GetNewDataType(dataTypeMappings, oldArg);

                                    dataTypeDict.Add(oldArg, newArg.Trim());
                                }
                                else
                                {
                                    newArg = dataTypeDict[oldArg];
                                }

                                break;

                            case "DATE":
                            case "DATE1":
                            case "DATE2":

                                newArg = DatetimeHelper.DecorateDatetimeString(targetDbType, newArg);

                                break;
                            case "UNIT":
                            case "'UNIT'":

                                newArg = DatetimeHelper.GetMappedUnit(sourceDbType, targetDbType, oldArg);

                                break;
                        }

                        return GetFunctionValue(targetFuncSpecs, newArg);
                    }

                    return string.Empty;
                };

                string GetTrimedContent(string content)
                {
                    return content.Trim('\'');
                }

                bool IsQuoted(string content)
                {
                    return content.StartsWith("\'");
                }

                var defaults = GetFunctionDefaults(targetFunctionInfo);
                var targetFunctionName = targetFunctionInfo.Name;

                if (sourceDbInterpreter.DatabaseType == DatabaseType.Postgres)
                {
                    if (name == "TRIM" && formulaArgs.Count > 1)
                    {
                        switch (formulaArgs[0])
                        {
                            case "LEADING":
                                targetFunctionName = "LTRIM";
                                break;
                            case "TRAILING":
                                targetFunctionName = "RTRIM";
                                break;
                        }
                    }
                }

                if (!ignore)
                {
                    var sbArgs = new StringBuilder();

                    foreach (var tai in targetArgItems)
                    {
                        if (tai.Index > 0)
                        {
                            sbArgs.Append(targetFuncSpec.Delimiter == "," ? "," : $" {targetFuncSpec.Delimiter} ");
                        }

                        var content = tai.Content;
                        var trimedContent = GetTrimedContent(content);

                        var sourceItem =
                            sourceArgItems.FirstOrDefault(item => GetTrimedContent(item.Content) == trimedContent);

                        if (sourceItem != null)
                        {
                            var value = getSourceArg(sourceItem, content);

                            if (!string.IsNullOrEmpty(value))
                            {
                                if (IsQuoted(sourceItem.Content) && !IsQuoted(content))
                                {
                                    value = GetTrimedContent(value);
                                }

                                if (content.StartsWith("\'"))
                                {
                                    sbArgs.Append('\'');
                                }

                                sbArgs.Append(value);

                                if (content.EndsWith("\'"))
                                {
                                    sbArgs.Append('\'');
                                }
                            }
                            else
                            {
                                var defaultValue = GetFunctionDictionaryValue(defaults, content);

                                if (!string.IsNullOrEmpty(defaultValue))
                                {
                                    sbArgs.Append(defaultValue);
                                }
                            }
                        }
                        else if (sourceArgItems.Any(item =>
                                     item.Details.Any(t => GetTrimedContent(t.Content) == trimedContent)))
                        {
                            var sd = sourceArgItems.FirstOrDefault(item =>
                                item.Details.Any(t => GetTrimedContent(t.Content) == trimedContent));

                            var details = sd.Details;

                            if (formulaArgs.Count > sd.Index)
                            {
                                var args = formulaArgs[sd.Index]
                                    .SplitByString(" ", StringSplitOptions.RemoveEmptyEntries);

                                if (details.Where(item => item.Type != FunctionArgumentItemDetailType.Whitespace)
                                        .Count() == args.Length)
                                {
                                    var i = 0;
                                    foreach (var detail in details)
                                    {
                                        if (detail.Type != FunctionArgumentItemDetailType.Whitespace)
                                        {
                                            if (GetTrimedContent(detail.Content) == trimedContent)
                                            {
                                                sbArgs.Append(args[i]);
                                                break;
                                            }

                                            i++;
                                        }
                                    }
                                }
                            }
                        }
                        else if (content == "START")
                        {
                            sbArgs.Append("0");
                        }
                        else if (tai.Details.Count > 0)
                        {
                            foreach (var detail in tai.Details)
                            {
                                var dc = detail.Content;
                                var trimedDc = GetTrimedContent(dc);

                                var si = sourceArgItems.FirstOrDefault(item =>
                                    GetTrimedContent(item.Content) == trimedDc);

                                if (si != null)
                                {
                                    var value = getSourceArg(si, detail.Content);

                                    if (IsQuoted(si.Content) && !IsQuoted(dc))
                                    {
                                        value = GetTrimedContent(value);
                                    }

                                    if (dc.StartsWith("\'"))
                                    {
                                        sbArgs.Append('\'');
                                    }

                                    sbArgs.Append(value);

                                    if (dc.EndsWith("\'"))
                                    {
                                        sbArgs.Append('\'');
                                    }
                                }
                                else
                                {
                                    sbArgs.Append(detail.Content);
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(targetFunctionInfo.Args))
                        {
                            sbArgs.Append(content);
                        }
                        else if (defaults.TryGetValue(content, out var @default))
                        {
                            sbArgs.Append(@default);
                        }
                        else
                        {
                            sbArgs.Append(content);
                        }
                    }

                    #region Oracle: use TO_CHAR instead of CAST(xxx as varchar2(n))

                    if (targetDbType == DatabaseType.Oracle)
                    {
                        if (targetFunctionName == "CAST")
                        {
                            var items = sbArgs.ToString().SplitByString("AS");
                            var dataType = items.LastOrDefault().Trim();

                            if (DataTypeHelper.IsCharType(dataType))
                            {
                                if (routineType == RoutineType.PROCEDURE || routineType == RoutineType.FUNCTION ||
                                    routineType == RoutineType.TRIGGER)
                                {
                                    targetFunctionName = "TO_CHAR";
                                    sbArgs.Clear();
                                    sbArgs.Append(items[0].Trim());
                                }
                            }
                        }
                    }

                    #endregion

                    newExpression = $"{targetFunctionName}{(targetFuncSpec.NoParenthesess ? "" : $"({sbArgs})")}";
                }
                else
                {
                    if (!string.IsNullOrEmpty(targetFunctionInfo.Expression))
                    {
                        var expression = targetFunctionInfo.Expression;

                        foreach (var sourceItem in sourceArgItems)
                        {
                            var value = getSourceArg(sourceItem, sourceItem.Content);

                            if (string.IsNullOrEmpty(value))
                            {
                                var defaultValue = GetFunctionDictionaryValue(defaults, sourceItem.Content);

                                if (!string.IsNullOrEmpty(defaultValue))
                                {
                                    value = defaultValue;
                                }
                            }

                            expression = expression.Replace(sourceItem.Content, value);
                        }

                        newExpression = expression;
                    }
                }

                var replacements = GetFunctionStringDictionary(targetFunctionInfo.Replacements);

                foreach (var replacement in replacements)
                {
                    newExpression = newExpression.Replace(replacement.Key, replacement.Value);
                }
            }

            dictDataType = dataTypeDict;

            return newExpression;
        }

        private string GetFunctionDictionaryValue(Dictionary<string, string> values, string arg)
        {
            if (values.TryGetValue(arg, out var value))
            {
                return value;
            }

            return null;
        }

        private Dictionary<string, string> GetFunctionDefaults(MappingFunctionInfo targetFunctionInfo)
        {
            return GetFunctionStringDictionary(targetFunctionInfo.Defaults);
        }

        private Dictionary<string, string> GetFunctionReplacements(MappingFunctionInfo targetFunctionInfo)
        {
            return GetFunctionStringDictionary(targetFunctionInfo.Replacements);
        }

        private Dictionary<string, string> GetFunctionStringDictionary(string content)
        {
            var dict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(content))
            {
                var items = content.Split(';');

                foreach (var item in items)
                {
                    var subItems = item.Split(':');

                    if (subItems.Length == 2)
                    {
                        var key = subItems[0];
                        var value = subItems[1];

                        if (!dict.ContainsKey(key))
                        {
                            dict.Add(key, value);
                        }
                    }
                }
            }

            return dict;
        }

        private string GetFunctionValue(List<FunctionSpecification> targetFuncSpecs, string value)
        {
            var targetFuncSpec =
                targetFuncSpecs.FirstOrDefault(item => item.Name.ToUpper() == value.TrimEnd('(', ')').ToUpper());

            if (targetFuncSpec != null)
            {
                if (targetFuncSpec.NoParenthesess && value.EndsWith("()"))
                {
                    value = value.Substring(0, value.Length - 2);
                }
            }

            return value;
        }

        internal static List<FunctionArgumentItemInfo> GetFunctionArgumentTokens(FunctionSpecification spec,
            string functionArgs)
        {
            var itemInfos = new List<FunctionArgumentItemInfo>();

            var specArgs = string.IsNullOrEmpty(functionArgs) ? spec.Args : functionArgs;

            if (!specArgs.EndsWith("..."))
            {
                var str = Regex.Replace(specArgs, @"[\[\]]", "");

                var items = str.Split(new[] { spec.Delimiter }, StringSplitOptions.RemoveEmptyEntries);

                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i].Trim();

                    var itemInfo = new FunctionArgumentItemInfo { Index = i, Content = item };

                    if (item.Contains(" "))
                    {
                        var details = item.SplitByString(" ", StringSplitOptions.RemoveEmptyEntries);

                        for (var j = 0; j < details.Length; j++)
                        {
                            if (j > 0)
                            {
                                itemInfo.Details.Add(new FunctionArgumentItemDetailInfo
                                    { Type = FunctionArgumentItemDetailType.Whitespace, Content = " " });
                            }

                            var detail = new FunctionArgumentItemDetailInfo
                                { Type = FunctionArgumentItemDetailType.Text, Content = details[j] };

                            itemInfo.Details.Add(detail);
                        }
                    }

                    itemInfos.Add(itemInfo);
                }
            }

            return itemInfos;
        }

        public MappingFunctionInfo GetMappingFunctionInfo(string name, string args, out bool useBrackets)
        {
            useBrackets = false;

            var text = name;
            var textWithBrackets = name.ToLower() + "()";

            if (functionMappings.Any(item => item.Any(t => t.Function.ToLower() == textWithBrackets)))
            {
                text = textWithBrackets;
                useBrackets = true;
            }

            var functionInfo = new MappingFunctionInfo { Name = name };

            var funcMappings = functionMappings.FirstOrDefault(item =>
                item.Any(t =>
                    (t.Direction == FunctionMappingDirection.OUT || t.Direction == FunctionMappingDirection.INOUT)
                    && t.DbType == sourceDbInterpreter.DatabaseType.ToString()
                    && t.Function.Split(',').Any(m => m.ToLower() == text.ToLower())
                )
            );

            var mapping = funcMappings?.FirstOrDefault(item =>
                (item.Direction == FunctionMappingDirection.IN || item.Direction == FunctionMappingDirection.INOUT)
                && item.DbType == targetDbInterpreter.DatabaseType.ToString());

            if (mapping != null)
            {
                var matched = true;

                if (!string.IsNullOrEmpty(args) && !string.IsNullOrEmpty(mapping.Args))
                {
                    if (mapping.IsFixedArgs && args.Trim().ToLower() != mapping.Args.Trim().ToLower())
                    {
                        matched = false;
                    }
                }

                if (matched)
                {
                    functionInfo.Name = mapping.Function.Split(',')?.FirstOrDefault();
                    functionInfo.Args = mapping.Args;
                    functionInfo.IsFixedArgs = mapping.IsFixedArgs;
                    functionInfo.Expression = mapping.Expression;
                    functionInfo.Defaults = mapping.Defaults;
                    functionInfo.Translator = mapping.Translator;
                    functionInfo.Specials = mapping.Specials;
                    functionInfo.Replacements = mapping.Replacements;
                }
            }

            return functionInfo;
        }

        public string FormatSql(string sql, out bool hasError)
        {
            hasError = false;

            var manager = new SqlFormattingManager();

            var formattedSql = manager.Format(sql, ref hasError);

            return formattedSql;
        }

        protected string GetTrimmedName(string name)
        {
            return name?.Trim(sourceDbInterpreter.QuotationLeftChar, sourceDbInterpreter.QuotationRightChar,
                targetDbInterpreter.QuotationLeftChar, targetDbInterpreter.QuotationRightChar, '"');
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public void Feedback(FeedbackInfoType infoType, string message, bool skipError = false)
        {
            if (observer != null)
            {
                var info = new FeedbackInfo
                {
                    Owner = this, InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(message),
                    IgnoreError = skipError
                };

                FeedbackHelper.Feedback(observer, info);
            }
        }

        public void FeedbackInfo(string message)
        {
            Feedback(FeedbackInfoType.Info, message);
        }

        public void FeedbackError(string message, bool skipError = false)
        {
            Feedback(FeedbackInfoType.Error, message, skipError);
        }
    }
}