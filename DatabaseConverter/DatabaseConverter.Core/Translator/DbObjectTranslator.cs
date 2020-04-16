﻿using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using PoorMansTSqlFormatterRedux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatabaseConverter.Core
{
    public abstract class DbObjectTranslator
    {
        protected string sourceOwnerName;
        protected DbInterpreter sourceDbInterpreter;
        protected DbInterpreter targetDbInterpreter;
        protected List<DataTypeMapping> dataTypeMappings = new List<DataTypeMapping>();
        protected List<IEnumerable<FunctionMapping>> functionMappings = new List<IEnumerable<FunctionMapping>>();
        protected List<IEnumerable<VariableMapping>> variableMappings = new List<IEnumerable<VariableMapping>>();

        public bool SkipError { get; set; }

        public TranslateHandler OnTranslated;

        public DbObjectTranslator(DbInterpreter source, DbInterpreter target)
        {
            this.sourceDbInterpreter = source;
            this.targetDbInterpreter = target;
        }

        public DbObjectTranslator LoadMappings()
        {
            if (this.sourceDbInterpreter.DatabaseType != this.targetDbInterpreter.DatabaseType)
            {
                this.functionMappings = FunctionMappingManager.GetFunctionMappings();
                this.variableMappings = VariableMappingManager.GetVariableMappings();
                this.dataTypeMappings = DataTypeMappingManager.GetDataTypeMappings(this.sourceDbInterpreter.DatabaseType, this.targetDbInterpreter.DatabaseType);
            }

            return this;
        }

        public abstract void Translate();

        public DataTypeMapping GetDataTypeMapping(List<DataTypeMapping> mappings, string dataType)
        {
            return mappings.FirstOrDefault(item => item.Source.Type?.ToLower() == dataType?.ToLower());
        }

        public string GetNewDataType(List<DataTypeMapping> mappings, string dataType, bool usedForFunction = true)
        {
            dataType = dataType.Trim();

            DatabaseType sourceDbType = this.sourceDbInterpreter.DatabaseType;
            DatabaseType targetDbType = this.targetDbInterpreter.DatabaseType;

            string cleanDataType = dataType.Split('(')[0];
            string newDataType = cleanDataType;
            bool hasPrecisionScale = false;

            if (cleanDataType != dataType)
            {
                hasPrecisionScale = true;
            }

            string upperTypeName = newDataType.ToUpper();

            DataTypeMapping mapping = this.GetDataTypeMapping(mappings, cleanDataType);

            if (mapping != null)
            {
                DataTypeMappingTarget targetDataType = mapping.Tareget;
                newDataType = targetDataType.Type;

                if (usedForFunction)
                {
                    if (targetDbType == DatabaseType.MySql)
                    {
                        if (upperTypeName == "INT")
                        {
                            newDataType = "SIGNED";
                        }
                        else if (upperTypeName == "FLOAT" || upperTypeName == "DOUBLE" || upperTypeName == "NUMBER")
                        {
                            newDataType = "DECIMAL";
                        }
                        else if (DataTypeHelper.IsCharType(newDataType))
                        {
                            newDataType = "CHAR";
                        }
                    }
                }

                if (!hasPrecisionScale && !string.IsNullOrEmpty(targetDataType.Precision) && !string.IsNullOrEmpty(targetDataType.Scale))
                {
                    newDataType += $"({targetDataType.Precision},{targetDataType.Scale})";
                }
                else if (hasPrecisionScale)
                {
                    newDataType += "(" + dataType.Split('(')[1];
                }
            }
            else
            {
                if (usedForFunction)
                {
                    if (sourceDbType == DatabaseType.MySql)
                    {
                        if (upperTypeName == "SIGNED")
                        {
                            if (targetDbType == DatabaseType.SqlServer)
                            {
                                newDataType = "DECIMAL";
                            }
                            else if (targetDbType == DatabaseType.Oracle)
                            {
                                newDataType = "NUMBER";
                            }
                        }
                    }
                }
            }

            return newDataType;
        }

        public string FormatSql(string sql, out bool hasError)
        {
            hasError = false;

            SqlFormattingManager manager = new SqlFormattingManager();

            string formattedSql = manager.Format(sql, ref hasError);

            return formattedSql;
        }

        public string ReplaceValue(string source, string oldValue, string newValue, RegexOptions option = RegexOptions.IgnoreCase)
        {
            return Regex.Replace(source, Regex.Escape(oldValue), newValue, option);
        }

        public string ExchangeFunctionArgs(string functionName, string args1, string args2)
        {
            if (functionName.ToUpper() == "CONVERT" && this.targetDbInterpreter.DatabaseType == DatabaseType.MySql && args1.ToUpper().Contains("DATE"))
            {
                if (args2.Contains(','))
                {
                    args2 = args2.Split(',')[0];
                }
            }

            string newExpression = $"{functionName}({args2},{args1})";

            return newExpression;
        }

        public string ReplaceVariables(string script)
        {
            foreach (IEnumerable<VariableMapping> mapping in this.variableMappings)
            {
                VariableMapping sourceVariable = mapping.FirstOrDefault(item => item.DbType == this.sourceDbInterpreter.DatabaseType.ToString());
                VariableMapping targetVariable = mapping.FirstOrDefault(item => item.DbType == this.targetDbInterpreter.DatabaseType.ToString());

                if (sourceVariable != null && !string.IsNullOrEmpty(sourceVariable.Variable) && targetVariable.Variable != null && !string.IsNullOrEmpty(targetVariable.Variable))
                {
                    script = this.ReplaceValue(script, sourceVariable.Variable, targetVariable.Variable);
                }
            }

            return script;
        }

        public string ParseFomular(List<FunctionSpecification> sourceFuncSpecs, List<FunctionSpecification> targetFuncSpecs,
            FunctionFomular fomular, string targetFunctionName, out Dictionary<string, string> dictDataType)
        {
            dictDataType = new Dictionary<string, string>();

            string name = fomular.Name;

            FunctionSpecification sourceFuncSpec = sourceFuncSpecs.FirstOrDefault(item => item.Name.ToUpper() == name.ToUpper());
            FunctionSpecification targetFuncSpec = targetFuncSpecs.FirstOrDefault(item => item.Name.ToUpper() == targetFunctionName.ToUpper());

            string newExpression = fomular.Expression;

            if (sourceFuncSpec != null && targetFuncSpec != null)
            {
                Dictionary<int, string> targetTokens = this.GetFunctionArgumentTokens(targetFuncSpec);
                Dictionary<int, string> sourceTokens = this.GetFunctionArgumentTokens(sourceFuncSpec);

                bool ignore = false;

                if(fomular.Args.Count >0 && (targetTokens.Count == 0 || sourceTokens.Count==0))
                {
                    ignore = true;
                }

                if(!ignore)
                {
                    string delimiter = sourceFuncSpec.Delimiter == "," ? "," : $" {sourceFuncSpec.Delimiter} ";

                    fomular.Delimiter = delimiter;

                    List<string> args = new List<string>();

                    foreach (var kp in targetTokens)
                    {
                        int targetIndex = kp.Key;
                        string token = kp.Value;

                        if (sourceTokens.ContainsValue(token))
                        {
                            int sourceIndex = sourceTokens.FirstOrDefault(item => item.Value == token).Key;

                            if (fomular.Args.Count > sourceIndex)
                            {
                                string oldArg = fomular.Args[sourceIndex];
                                string newArg = oldArg;

                                switch (token.ToUpper())
                                {
                                    case "TYPE":

                                        if (!dictDataType.ContainsKey(oldArg))
                                        {
                                            newArg = this.GetNewDataType(this.dataTypeMappings, oldArg);

                                            dictDataType.Add(oldArg, newArg.Trim());
                                        }
                                        else
                                        {
                                            newArg = dictDataType[oldArg];
                                        }
                                        break;
                                }

                                args.Add(newArg);
                            }
                        }
                    }

                    string targetDelimiter = targetFuncSpec.Delimiter == "," ? "," : $" {targetFuncSpec.Delimiter} ";

                    string strArgs = string.Join(targetDelimiter, args);

                    newExpression = $"{targetFunctionName}{ (targetFuncSpec.NoParenthesess ? "" : $"({strArgs})") }";
                }                
            }

            return newExpression;
        }

        public Dictionary<int, string> GetFunctionArgumentTokens(FunctionSpecification spec)
        {
            Dictionary<int, string> dictTokenIndex = new Dictionary<int, string>();

            if (!(spec.Args.EndsWith(",...") || spec.Args.Contains("[")))
            {
                string[] args = spec.Args.Split(new string[] { spec.Delimiter }, StringSplitOptions.RemoveEmptyEntries);

                int i = 0;

                foreach (string arg in args)
                {
                    dictTokenIndex.Add(i, arg.Trim());

                    i++;
                }
            }

            return dictTokenIndex;
        }

        public string GetMappedFunctionName(string name)
        {
            string text = name;
            string textWithBrackets = name.ToLower() + "()";

            if (this.functionMappings.Any(item => item.Any(t => t.Function.ToLower() == textWithBrackets)))
            {
                text = textWithBrackets;
            }

            string targetFunctionName = name;

            IEnumerable<FunctionMapping> funcMappings = this.functionMappings.FirstOrDefault(item => item.Any(t =>
             (t.Direction == FunctionMappingDirection.OUT || t.Direction == FunctionMappingDirection.INOUT)
              && t.DbType == sourceDbInterpreter.DatabaseType.ToString() && t.Function.Split(',').Any(m => m.ToLower() == text.ToLower())));

            if (funcMappings != null)
            {
                targetFunctionName = funcMappings.FirstOrDefault(item =>
                        (item.Direction == FunctionMappingDirection.IN || item.Direction == FunctionMappingDirection.INOUT)
                        && item.DbType == targetDbInterpreter.DatabaseType.ToString())?.Function.Split(',')?.FirstOrDefault();

                if (string.IsNullOrEmpty(targetFunctionName))
                {
                    targetFunctionName = name;
                }
            }

            return targetFunctionName;
        }
    }
}
