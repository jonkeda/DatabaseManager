using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Databases.Config;
using Databases.Converter.Helper;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Model.DatabaseObject;
using Databases.Model.DataType;
using Databases.Model.Enum;
using Databases.Model.Function;
using Databases.Model.Option;

namespace Databases.Converter.Translator
{
    public class ColumnTranslator : DbObjectTokenTranslator
    {
        private readonly IEnumerable<TableColumn> columns;
        private readonly DataTypeTranslator dataTypeTranslator;
        private readonly FunctionTranslator functionTranslator;
        private readonly SequenceTranslator sequenceTranslator;
        private readonly IEnumerable<DataTypeSpecification> sourceDataTypeSpecs;
        private List<FunctionSpecification> targetFuncSpecs;

        public ColumnTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            IEnumerable<TableColumn> columns) : base(sourceInterpreter, targetInterpreter)
        {
            this.columns = columns;
            sourceDataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(sourceDbType);
            functionTranslator = new FunctionTranslator(sourceDbInterpreter, targetDbInterpreter);
            dataTypeTranslator = new DataTypeTranslator(sourceDbInterpreter, targetDbInterpreter);
            sequenceTranslator = new SequenceTranslator(sourceDbInterpreter, targetDbInterpreter);
            targetFuncSpecs = FunctionManager.GetFunctionSpecifications(targetDbType);
        }

        public List<TableColumn> ExistedTableColumns { get; set; }

        public override void Translate()
        {
            if (sourceDbType == targetDbType)
            {
                return;
            }

            if (hasError)
            {
                return;
            }

            if (!columns.Any())
            {
                return;
            }

            FeedbackInfo("Begin to translate columns.");

            LoadMappings();
            functionTranslator.LoadMappings();
            functionTranslator.LoadFunctionSpecifications();
            sequenceTranslator.Option = Option;
            dataTypeTranslator.Option = Option;

            var dataModeOnly = Option.GenerateScriptMode == GenerateScriptMode.Data;

            if (!dataModeOnly)
            {
                CheckComputeExpression();
            }

            foreach (var column in columns)
            {
                var existedColumn = GetExistedColumn(column);

                if (existedColumn != null)
                {
                    column.DataType = existedColumn.DataType;
                }
                else
                {
                    if (!DataTypeHelper.IsUserDefinedType(column))
                    {
                        TranslateHelper.TranslateTableColumnDataType(dataTypeTranslator, column);
                    }
                }

                if (!dataModeOnly)
                {
                    if (!string.IsNullOrEmpty(column.DefaultValue))
                    {
                        ConvertDefaultValue(column);
                    }

                    if (column.IsComputed)
                    {
                        ConvertComputeExpression(column);
                    }
                }
            }

            FeedbackInfo("End translate columns.");
        }

        private TableColumn GetExistedColumn(TableColumn column)
        {
            if (ExistedTableColumns == null || ExistedTableColumns.Count == 0)
            {
                return null;
            }

            return ExistedTableColumns.FirstOrDefault(
                item => SchemaInfoHelper.IsSameTableColumnIgnoreCase(item, column));
        }

        public void ConvertDefaultValue(TableColumn column)
        {
            var defaultValue = StringHelper.GetBalanceParenthesisTrimedValue(column.DefaultValue);
            var hasParenthesis = defaultValue != column.DefaultValue;

            if (defaultValue.ToUpper() == "NULL")
            {
                column.DefaultValue = null;
                return;
            }

            Func<string> getTrimedValue = () => { return defaultValue.Trim().Trim('\''); };

            var trimedValue = getTrimedValue();

            if (SequenceTranslator.IsSequenceValueFlag(sourceDbType, defaultValue))
            {
                if (targetDbType == DatabaseType.MySql
                    || Option.OnlyForTableCopy
                    || column.IsIdentity)
                {
                    column.DefaultValue = null;
                }
                else
                {
                    column.DefaultValue = sequenceTranslator.HandleSequenceValue(defaultValue);
                }

                return;
            }

            if (defaultValue == "''")
            {
                if (targetDbType == DatabaseType.Oracle)
                {
                    column.IsNullable = true;
                    return;
                }
            }

            var hasScale = false;
            string scale = null;

            if (DataTypeHelper.IsDateOrTimeType(column.DataType) && defaultValue.Count(item => item == '(') == 1 &&
                defaultValue.EndsWith(")")) //timestamp(scale)
            {
                var index = defaultValue.IndexOf('(');
                hasScale = true;
                scale = defaultValue.Substring(index + 1).Trim(')').Trim();
                defaultValue = defaultValue.Substring(0, index).Trim();
            }

            var functionName = defaultValue;

            var formulas = FunctionTranslator.GetFunctionFormulas(sourceDbInterpreter, defaultValue);

            if (formulas.Count > 0)
            {
                functionName = formulas.First().Name;
            }

            var funcMappings = functionMappings.FirstOrDefault(item => item.Any(t => t.DbType == sourceDbType.ToString()
                && t.Function.Split(',').Any(m => m.Trim().ToLower() == functionName.ToLower())));

            if (funcMappings != null)
            {
                functionName = funcMappings.FirstOrDefault(item => item.DbType == targetDbType.ToString())?.Function
                    .Split(',')?.FirstOrDefault();

                var handled = false;

                if (targetDbType == DatabaseType.MySql || targetDbType == DatabaseType.Postgres)
                {
                    if (functionName.ToUpper() == "CURRENT_TIMESTAMP" && column.DataType.Contains("timestamp") &&
                        column.Scale > 0)
                    {
                        defaultValue = $"{functionName}({column.Scale})";
                        handled = true;
                    }
                }

                if (targetDbType == DatabaseType.SqlServer)
                {
                    if (functionName == "GETDATE")
                    {
                        if (hasScale && int.TryParse(scale, out _))
                        {
                            if (int.Parse(scale) > 3)
                            {
                                defaultValue = "SYSDATETIME";
                                handled = true;
                            }
                        }
                    }
                }

                if (!handled)
                {
                    defaultValue = functionTranslator.GetMappedFunction(defaultValue);
                }
            }
            else
            {
                if (sourceDbType == DatabaseType.Postgres)
                {
                    if (defaultValue.Contains("::")) //remove Postgres type reference
                    {
                        var items = defaultValue.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        var list = new List<string>();

                        foreach (var item in items)
                        {
                            list.Add(item.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)[0]);
                        }

                        defaultValue = string.Join(" ", list);
                        trimedValue = getTrimedValue();
                    }

                    if (defaultValue.Trim() == "true" || defaultValue.Trim() == "false")
                    {
                        defaultValue = defaultValue.Replace("true", "1").Replace("false", "0");
                    }

                    if (defaultValue.ToUpper().Contains("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'"))
                    {
                        if (targetDbType == DatabaseType.SqlServer)
                        {
                            defaultValue = "GETUTCDATE()";
                        }
                        else if (targetDbType == DatabaseType.MySql)
                        {
                            defaultValue = "UTC_TIMESTAMP()";
                        }
                        else if (targetDbType == DatabaseType.Oracle)
                        {
                            defaultValue = "SYS_EXTRACT_UTC(SYSTIMESTAMP)";
                        }
                    }
                }
                else if (sourceDbType == DatabaseType.SqlServer)
                {
                    //if it uses defined default value
                    if (defaultValue.Contains("CREATE DEFAULT ") && defaultValue.Contains(" AS "))
                    {
                        var asIndex = defaultValue.LastIndexOf("AS");
                        defaultValue = defaultValue.Substring(asIndex + 3).Trim();
                    }

                    if (trimedValue.ToLower() == "newid()")
                    {
                        column.DefaultValue = null;

                        return;
                    }
                }

                #region handle date/time string

                if (sourceDbType == DatabaseType.SqlServer)
                {
                    //target data type is datetime or timestamp, but default value is time ('HH:mm:ss')
                    if (DataTypeHelper.IsDatetimeOrTimestampType(column.DataType))
                    {
                        if (TimeSpan.TryParse(trimedValue, out _))
                        {
                            if (targetDbType == DatabaseType.MySql || targetDbType == DatabaseType.Postgres)
                            {
                                defaultValue = $"'{DateTime.MinValue.Date.ToLongDateString()} {trimedValue}'";
                            }
                            else if (targetDbType == DatabaseType.Oracle)
                            {
                                defaultValue =
                                    $"TO_TIMESTAMP('{DateTime.MinValue.Date.ToString("yyyy-MM-dd")} {TimeSpan.Parse(trimedValue).ToString("HH:mm:ss")}','yyyy-MM-dd hh24:mi:ss')";
                            }
                        }
                    }
                }
                else if (sourceDbType == DatabaseType.Oracle)
                {
                    if (DataTypeHelper.IsDateOrTimeType(column.DataType) && trimedValue.StartsWith("TO_TIMESTAMP"))
                    {
                        var index = trimedValue.IndexOf('(');

                        defaultValue = defaultValue.Split(',')[0].Substring(index + 1);
                    }
                }
                else if (sourceDbType == DatabaseType.MySql)
                {
                    if (trimedValue == "0000-00-00 00:00:00")
                    {
                        column.DefaultValue = null;
                        return;
                    }

                    if (targetDbType == DatabaseType.SqlServer || targetDbType == DatabaseType.Postgres)
                    {
                        defaultValue = $"'{trimedValue}'";
                    }
                }
                else if (sourceDbType == DatabaseType.Postgres)
                {
                    defaultValue = defaultValue.Replace("without time zone", "");
                    trimedValue = getTrimedValue();
                }

                if (DataTypeHelper.IsDateOrTimeType(column.DataType))
                {
                    if (targetDbType == DatabaseType.Oracle) //datetime string to TO_TIMESTAMP(value)
                    {
                        if (DateTime.TryParse(trimedValue, out _))
                        {
                            if (trimedValue.Contains(" ")) // date & time
                            {
                                defaultValue =
                                    $"TO_TIMESTAMP('{DateTime.Parse(trimedValue).ToString("yyyy-MM-dd HH:mm:ss")}','yyyy-MM-dd hh24:mi:ss')";
                            }
                            else
                            {
                                if (trimedValue.Contains(":")) //time
                                {
                                    defaultValue =
                                        $"TO_TIMESTAMP('{DateTime.MinValue.ToString("yyyy-MM-dd")} {DateTime.Parse(trimedValue).ToString("HH:mm:ss")}','yyyy-MM-dd hh24:mi:ss')";
                                }
                                else //date
                                {
                                    defaultValue =
                                        $"TO_TIMESTAMP('{DateTime.Parse(trimedValue).ToString("yyyy-MM-dd")}','yyyy-MM-dd')";
                                }
                            }
                        }
                    }
                }

                #endregion

                #region handle binary type

                if (sourceDbType == DatabaseType.SqlServer)
                {
                    if (targetDbType == DatabaseType.Postgres)
                    {
                        if (column.DataType == "bytea" && column.MaxLength > 0)
                        {
                            long value = 0;

                            if (long.TryParse(defaultValue, out value))
                            {
                                //integer hex string to bytea
                                var hex = value.ToString("X").PadLeft((int)column.MaxLength.Value * 2, '0');

                                defaultValue = $"'\\x{hex}'::bytea";
                            }
                            else if (defaultValue.StartsWith("0x"))
                            {
                                defaultValue = $"'\\x{defaultValue.Substring(2)}'::bytea";
                            }
                        }
                    }
                    else if (targetDbType == DatabaseType.Oracle || targetDbType == DatabaseType.MySql)
                    {
                        if (DataTypeHelper.IsBinaryType(column.DataType) && column.MaxLength > 0)
                        {
                            long value = 0;

                            if (long.TryParse(defaultValue, out value))
                            {
                                var hex = value.ToString("X").PadLeft((int)column.MaxLength.Value * 2, '0');

                                if (targetDbType == DatabaseType.Oracle)
                                {
                                    defaultValue = $"'{hex}'";
                                }
                                else if (targetDbType == DatabaseType.MySql)
                                {
                                    defaultValue = $"0x{hex}";
                                }
                            }
                            else if (defaultValue.StartsWith("0x"))
                            {
                                if (targetDbType == DatabaseType.Oracle)
                                {
                                    defaultValue = $"'{defaultValue.Substring(2)}'";
                                }
                            }
                        }
                    }
                }
                else if (sourceDbType == DatabaseType.Postgres)
                {
                    if (DataTypeHelper.IsBinaryType(column.DataType) && trimedValue.StartsWith("\\x"))
                    {
                        if (targetDbType == DatabaseType.SqlServer || targetDbType == DatabaseType.MySql)
                        {
                            defaultValue = $"0x{trimedValue.Substring(2)}";
                        }
                        else if (targetDbType == DatabaseType.Oracle)
                        {
                            defaultValue = $"'{trimedValue.Substring(2)}'";
                        }
                    }
                }
                else if (sourceDbType == DatabaseType.MySql)
                {
                    if (DataTypeHelper.IsBinaryType(column.DataType))
                    {
                        if (trimedValue == "0x")
                        {
                            //when type is binary(10) and default value is 0x00000000000000000001 or b'00000000000000000001',
                            //the column "COLUMN_DEFAULT" of "INFORMATION_SCHEMA.COLUMNS" always is "0x", but use "insert into table values(default)" is correct result.

                            column.DefaultValue = null; //TODO                            
                            return;
                        }

                        if (trimedValue.StartsWith("0x"))
                        {
                            if (targetDbType == DatabaseType.Postgres)
                            {
                                defaultValue = $"'\\x{trimedValue.Substring(2)}'::bytea";
                            }
                            else if (targetDbType == DatabaseType.Oracle)
                            {
                                defaultValue = $"'{trimedValue.Substring(2)}'";
                            }
                        }
                    }
                }
                else if (sourceDbType == DatabaseType.Oracle)
                {
                    if (DataTypeHelper.IsBinaryType(column.DataType))
                    {
                        if (targetDbType == DatabaseType.SqlServer || targetDbType == DatabaseType.MySql)
                        {
                            defaultValue = $"0x{trimedValue}";
                        }

                        if (targetDbType == DatabaseType.Postgres && column.DataType == "bytea")
                        {
                            defaultValue = $"'\\x{trimedValue}'::bytea";
                        }
                    }
                }

                #endregion

                if (column.DataType == "boolean")
                {
                    if (targetDbType == DatabaseType.Postgres)
                    {
                        defaultValue = defaultValue.Replace("0", "false").Replace("1", "true");
                    }
                }
                else if (DataTypeHelper.IsUserDefinedType(column))
                {
                    var udt = UserDefinedTypes.FirstOrDefault(item => item.Name == column.DataType);

                    if (udt != null)
                    {
                        var attr = udt.Attributes.FirstOrDefault();

                        var dataTypeInfo = new DataTypeInfo { DataType = attr.DataType };

                        dataTypeTranslator.Translate(dataTypeInfo);

                        if (targetDbType == DatabaseType.Postgres)
                        {
                            if (dataTypeInfo.DataType == "boolean")
                            {
                                defaultValue = defaultValue.Replace("0", "row(false)").Replace("1", "row(true)");
                            }
                        }
                        else if (targetDbType == DatabaseType.Oracle)
                        {
                            if ((attr.DataType == "bit" || attr.DataType == "boolean") &&
                                dataTypeInfo.DataType == "number")
                            {
                                var dataType = targetDbInterpreter.GetQuotedString(udt.Name);

                                defaultValue = defaultValue.Replace("0", $"{dataType}(0)")
                                    .Replace("1", $"{dataType}(1)");
                            }
                        }
                    }
                }

                //custom function
                if (defaultValue.Contains(sourceDbInterpreter.QuotationLeftChar) &&
                    sourceDbInterpreter.QuotationLeftChar != targetDbInterpreter.QuotationLeftChar)
                {
                    defaultValue = ParseDefinition(defaultValue);

                    if (defaultValue.Contains("."))
                    {
                        if (targetDbType == DatabaseType.MySql || targetDbType == DatabaseType.Oracle)
                        {
                            defaultValue = null;
                        }
                        else
                        {
                            var items = defaultValue.Split('.');
                            var schema = items[0].Trim();

                            if (Option.SchemaMappings.Any())
                            {
                                var mappedSchema = Option.SchemaMappings
                                    .FirstOrDefault(item => GetTrimmedName(item.SourceSchema) == GetTrimmedName(schema))
                                    ?.TargetSchema;

                                if (!string.IsNullOrEmpty(mappedSchema))
                                {
                                    defaultValue =
                                        $"{targetDbInterpreter.GetQuotedString(mappedSchema)}.{string.Join(".", items.Skip(1))}";
                                }
                                else
                                {
                                    defaultValue = string.Join(".", items.Skip(1));
                                }
                            }
                            else
                            {
                                defaultValue = string.Join(".", items.Skip(1));
                            }
                        }
                    }
                }
            }

            column.DefaultValue = !string.IsNullOrEmpty(defaultValue) && hasParenthesis && !defaultValue.StartsWith("-")
                ? $"({defaultValue})"
                : defaultValue;
        }

        public void ConvertComputeExpression(TableColumn column)
        {
            if (sourceDbType == DatabaseType.Oracle)
            {
                column.ComputeExp = column.ComputeExp.Replace("U'", "'");
            }
            else if (sourceDbType == DatabaseType.SqlServer)
            {
                column.ComputeExp = column.ComputeExp.Replace("N'", "'");
            }

            column.ComputeExp = ParseDefinition(column.ComputeExp);

            var computeExp = column.ComputeExp.ToLower();

            if (targetDbType == DatabaseType.Postgres)
                //this is to avoid error when datatype is money uses coalesce(exp,0)
            {
                if (computeExp.Contains("coalesce"))
                {
                    if (column.DataType.ToLower() == "money")
                    {
                        var exp = column.ComputeExp;

                        if (computeExp.StartsWith("("))
                        {
                            exp = exp.Substring(1, computeExp.Length - 1);
                        }

                        var formulas = FunctionTranslator.GetFunctionFormulas(sourceDbInterpreter, exp);

                        if (formulas.Count > 0)
                        {
                            var args = formulas.First().GetArgs();

                            if (args.Count > 0)
                            {
                                column.ComputeExp = args[0];
                            }
                        }
                    }
                }
            }

            if (targetDbType == DatabaseType.Postgres)
            {
                if (column.DataType == "money" && !column.ComputeExp.ToLower().Contains("::money"))
                {
                    column.ComputeExp = TranslateHelper.ConvertNumberToPostgresMoney(column.ComputeExp);
                }
            }

            if (sourceDbType == DatabaseType.Postgres)
            {
                if (column.ComputeExp.Contains("::")) //datatype convert operator
                {
                    column.ComputeExp = TranslateHelper.RemovePostgresDataTypeConvertExpression(column.ComputeExp,
                        sourceDataTypeSpecs, targetDbInterpreter.QuotationLeftChar,
                        targetDbInterpreter.QuotationRightChar);
                }
            }

            if (computeExp.Contains("concat", StringComparison.OrdinalIgnoreCase)) //use "||" instead of "concat"
            {
                if (targetDbType == DatabaseType.Postgres || targetDbType == DatabaseType.Oracle)
                {
                    column.ComputeExp = column.ComputeExp.ReplaceOrdinalIgnoreCase("concat", "").Replace(",", "||");

                    if (targetDbType == DatabaseType.Oracle)
                    {
                        column.ComputeExp = CheckColumnDataTypeForComputeExpression(column.ComputeExp,
                            targetDbInterpreter.STR_CONCAT_CHARS, targetDbType);
                    }
                }
            }

            if (!string.IsNullOrEmpty(sourceDbInterpreter.STR_CONCAT_CHARS))
            {
                var items = column.ComputeExp.SplitByString(sourceDbInterpreter.STR_CONCAT_CHARS);

                var charColumns = columns.Where(c => items.Any(item =>
                        GetTrimmedName(c.Name) == GetTrimmedName(item.Trim('(', ')')) &&
                        DataTypeHelper.IsCharType(c.DataType)))
                    .Select(c => c.Name);

                //if(this.Option.ConvertConcatChar)
                {
                    column.ComputeExp = ConcatCharsHelper.ConvertConcatChars(sourceDbInterpreter, targetDbInterpreter,
                        column.ComputeExp, charColumns);
                }

                column.ComputeExp = CheckColumnDataTypeForComputeExpression(column.ComputeExp,
                    targetDbInterpreter.STR_CONCAT_CHARS, targetDbType);
            }
        }

        private string CheckColumnDataTypeForComputeExpression(string computeExp, string concatChars,
            DatabaseType targetDbType)
        {
            if (sourceDbType == DatabaseType.Oracle)
            {
                computeExp = computeExp.Replace("SYS_OP_C2C", "").Replace("TO_CHAR", "");
            }

            //check whether column datatype is char/varchar type
            var items = computeExp.SplitByString(concatChars);

            var list = new List<string>();
            var changed = false;

            foreach (var item in items)
            {
                var trimedItem = item.Trim('(', ')', ' ');

                var col = columns.FirstOrDefault(c => GetTrimmedName(c.Name) == GetTrimmedName(trimedItem));

                if (col != null && !DataTypeHelper.IsCharType(col.DataType))
                {
                    changed = true;

                    if (targetDbType == DatabaseType.Oracle)
                    {
                        list.Add(item.Replace(trimedItem, $"TO_CHAR({trimedItem})"));
                    }
                    else if (targetDbType == DatabaseType.SqlServer)
                    {
                        list.Add(item.Replace(trimedItem, $"CAST({trimedItem} AS VARCHAR(MAX))"));
                    }
                }
                else
                {
                    list.Add(item);
                }
            }

            if (changed)
            {
                return string.Join(concatChars, list.ToArray());
            }

            return computeExp;
        }

        private async void CheckComputeExpression()
        {
            IEnumerable<Function> customFunctions = SourceSchemaInfo?.Functions;

            foreach (var column in columns)
            {
                if (column.IsComputed)
                {
                    if (Option != null && !Option.ConvertComputeColumnExpression)
                    {
                        if (Option.OnlyCommentComputeColumnExpressionInScript)
                        {
                            column.ScriptComment = " AS " + column.ComputeExp;
                        }

                        column.ComputeExp = null;
                        continue;
                    }

                    var setToNull = false;

                    var tableColumns = columns.Where(item => item.TableName == column.TableName);

                    var isReferToSpecialDataType = tableColumns.Any(item => item.Name != column.Name
                                                                            && DataTypeHelper.IsSpecialDataType(
                                                                                item.DataType)
                                                                            && Regex.IsMatch(column.ComputeExp,
                                                                                $@"\b({item.Name})\b",
                                                                                RegexOptions.IgnoreCase));

                    if (isReferToSpecialDataType)
                    {
                        setToNull = true;
                    }

                    if (!setToNull && (targetDbType == DatabaseType.MySql || targetDbType == DatabaseType.Oracle))
                    {
                        if (customFunctions == null || !customFunctions.Any())
                        {
                            customFunctions = await sourceDbInterpreter.GetFunctionsAsync();
                        }

                        if (customFunctions != null)
                        {
                            if (customFunctions.Any(item =>
                                    column.ComputeExp.IndexOf(item.Name, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                setToNull = true;
                            }
                        }
                    }

                    if (setToNull)
                    {
                        column.ScriptComment = " AS " + column.ComputeExp;
                        column.ComputeExp = null;
                    }
                }
            }
        }
    }
}