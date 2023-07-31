using System;
using Databases.Converter.Helper;
using Databases.Converter.Model;
using Databases.Converter.Model.Functions;
using Databases.Interpreter.Utility.Helper;
using Databases.Model.Enum;
using Databases.Model.Function;

namespace Databases.Converter.Translator.Functions
{
    public class DateDiffTranslator : SpecificFunctionTranslatorBase
    {
        public DateDiffTranslator(FunctionSpecification sourceSpecification, FunctionSpecification targetSpecification)
            : base(sourceSpecification, targetSpecification)
        { }


        public override string Translate(FunctionFormula formula)
        {
            var functionName = formula.Name;
            var targetFunctionName = TargetSpecification?.Name;
            var expression = formula.Expression;
            var delimiter = SourceSpecification.Delimiter ?? ",";
            var args = formula.GetArgs(delimiter);

            var argsReversed = false;

            var dateDiff = default(DateDiff?);

            var newExpression = expression;

            if (SourceDbType == DatabaseType.SqlServer)
            {
                dateDiff = new DateDiff { Unit = args[0], Date1 = args[2], Date2 = args[1] };

                argsReversed = true;
            }
            else if (SourceDbType == DatabaseType.MySql)
            {
                if (functionName == "DATEDIFF")
                {
                    dateDiff = new DateDiff { Unit = "DAY", Date1 = args[0], Date2 = args[1] };
                }
                else if (functionName == "TIMESTAMPDIFF")
                {
                    dateDiff = new DateDiff { Unit = args[0], Date1 = args[2], Date2 = args[1] };

                    argsReversed = true;
                }
            }

            if (dateDiff.HasValue)
            {
                var unit = dateDiff.Value.Unit.ToUpper();

                var isStringValue1 = ValueHelper.IsStringValue(dateDiff.Value.Date1);
                var isStringValue2 = ValueHelper.IsStringValue(dateDiff.Value.Date2);
                var date1 = dateDiff.Value.Date1;
                var date2 = dateDiff.Value.Date2;

                Action reverseDateArguments = () =>
                {
                    string temp;
                    temp = date1;
                    date1 = date2;
                    date2 = temp;
                };

                if (TargetDbType == DatabaseType.SqlServer)
                {
                    if (argsReversed)
                    {
                        reverseDateArguments();
                    }

                    newExpression = $"DATEDIFF({unit}, {date1},{date2})";
                }
                else if (TargetDbType == DatabaseType.MySql)
                {
                    if (targetFunctionName == "TIMESTAMPDIFF")
                    {
                        if (argsReversed)
                        {
                            reverseDateArguments();
                        }

                        newExpression = $"TIMESTAMPDIFF({unit}, {date1},{date2})";
                    }
                    else
                    {
                        newExpression = $"DATEDIFF({date1}, {date2})";
                    }
                }
                else if (TargetDbType == DatabaseType.Postgres)
                {
                    var dataType = "::TIMESTAMP";

                    var strDate1 = $"{date1}{dataType}";
                    var strDate2 = $"{date2}{dataType}";
                    var strDate1MinusData2 = $"{strDate1}-{strDate2}";

                    switch (unit)
                    {
                        case "YEAR":
                            newExpression = $"DATE_PART('YEAR', {strDate1}) - DATE_PART('YEAR', {strDate2})";
                            break;
                        case "MONTH":
                            newExpression =
                                $"(DATE_PART('YEAR', {strDate1}) - DATE_PART('YEAR', {strDate2})) * 12 +(DATE_PART('MONTH', {strDate1}) - DATE_PART('MONTH', {strDate2}))";
                            break;
                        case "WEEK":
                            newExpression = $"TRUNC(DATE_PART('DAY', {strDate1MinusData2})/7)";
                            break;
                        case "DAY":
                            newExpression = $"DATE_PART('{unit}',{strDate1MinusData2})";
                            break;
                        case "HOUR":
                            newExpression =
                                $"DATE_PART('DAY', {strDate1MinusData2}) * 24 +(DATE_PART('HOUR', {strDate1MinusData2}))";
                            break;
                        case "MINUTE":
                            newExpression =
                                $"(DATE_PART('DAY', {strDate1MinusData2}) * 24 + DATE_PART('HOUR', {strDate1MinusData2})) * 60 + DATE_PART('MINUTE', {strDate1MinusData2})";
                            break;
                        case "SECOND":
                            newExpression =
                                $"((DATE_PART('DAY', {strDate1MinusData2}) * 24 +  DATE_PART('HOUR', {strDate1MinusData2})) * 60 + DATE_PART('MINUTE', {strDate1MinusData2})) * 60 +  DATE_PART('SECOND', {strDate1MinusData2})";
                            break;
                    }
                }
                else if (TargetDbType == DatabaseType.Oracle)
                {
                    var isTimestampStr1 = isStringValue1 && DatetimeHelper.IsTimestampString(date1);
                    var isTimestampStr2 = isStringValue2 && DatetimeHelper.IsTimestampString(date2);
                    var isDateStr1 = isStringValue1 && !isTimestampStr1;
                    var isDateStr2 = isStringValue2 && !isTimestampStr2;

                    Func<string, bool, bool, string> getStrDate = (date, isStringValue, isTimestampStr) =>
                    {
                        if (isStringValue)
                        {
                            date = DatetimeHelper.GetOracleUniformDatetimeString(date, isTimestampStr);
                        }

                        var dataType = isStringValue ? isTimestampStr ? "TIMESTAMP" : "DATE" : "";

                        var strDate = $"{dataType}{date}";

                        if (!isTimestampStr)
                        {
                            strDate = $"CAST({strDate} AS TIMESTAMP)";
                        }

                        return strDate;
                    };

                    var strDate1 = getStrDate(date1, isStringValue1, isTimestampStr1);
                    var strDate2 = getStrDate(date2, isStringValue2, isTimestampStr2);

                    var strDate1MinusData2 = $"{strDate1}-{strDate2}";
                    var dateFormat = $"'{DatetimeHelper.DateFormat}'";
                    var datetimeFormat = $"'{DatetimeHelper.OracleDatetimeFormat}'";

                    Func<string, bool, bool, string> getDateFormatStr = (date, isDateStr, isTimestampStr) =>
                    {
                        if (isDateStr)
                        {
                            return $"TO_DATE({date}, {dateFormat})";
                        }

                        if (isTimestampStr)
                        {
                            return $"TO_DATE({date}, {datetimeFormat})";
                        }

                        return $"TO_DATE(TO_CHAR({date}, {datetimeFormat}), {datetimeFormat})";
                    };

                    Func<string, string> getDiffValue = multiplier =>
                    {
                        var value =
                            $"({getDateFormatStr(date1, isDateStr1, isTimestampStr1)}-{getDateFormatStr(date2, isDateStr2, isTimestampStr2)})";

                        return $"ROUND({value}*{multiplier})";
                    };

                    switch (unit)
                    {
                        case "YEAR":
                            newExpression = $"ROUND(EXTRACT(DAY FROM ({strDate1MinusData2}))/365)";
                            break;
                        case "MONTH":
                            newExpression = $"MONTHS_BETWEEN({strDate1},{strDate2})";
                            break;
                        case "WEEK":
                            newExpression = $"ROUND(EXTRACT(DAY FROM ({strDate1MinusData2}))/7)";
                            break;
                        case "DAY":
                            newExpression = $"EXTRACT(DAY FROM ({strDate1MinusData2}))";
                            break;
                        case "HOUR":
                            newExpression =
                                $"EXTRACT(DAY FROM ({strDate1MinusData2}))*24 + EXTRACT(HOUR FROM ({strDate1MinusData2}))";
                            break;
                        case "MINUTE":
                            //newExpression = $"(EXTRACT(DAY FROM {strDate1MinusData2}) * 24 + EXTRACT(HOUR FROM {strDate1MinusData2})) * 60 + EXTRACT(MINUTE FROM {strDate1MinusData2})";
                            newExpression = getDiffValue("24*60");
                            break;
                        case "SECOND":
                            //newExpression = $"((EXTRACT(DAY FROM {strDate1MinusData2}) * 24 +  EXTRACT(HOUR FROM {strDate1MinusData2})) * 60 + EXTRACT(MINUTE FROM {strDate1MinusData2})) * 60 +  EXTRACT(SECOND FROM {strDate1MinusData2})";
                            newExpression = getDiffValue("24*60*60");
                            break;
                    }
                }
                else if (TargetDbType == DatabaseType.Sqlite)
                {
                    Func<string, string> getDiffValue = multiplier =>
                    {
                        var value = $"(JULIANDAY({date1})-JULIANDAY({date2}))";

                        if (unit == "YEAR")
                        {
                            return $"FLOOR(ROUND({value}{multiplier},2))";
                        }

                        return $"ROUND({value}{multiplier})";
                    };

                    switch (unit)
                    {
                        case "YEAR":
                            newExpression = getDiffValue("/365");
                            break;
                        case "MONTH":
                            newExpression = getDiffValue("/30");
                            break;
                        case "WEEK":
                            newExpression = getDiffValue("/7");
                            break;
                        case "DAY":
                            newExpression = getDiffValue("");
                            break;
                        case "HOUR":
                            newExpression = getDiffValue("*24");
                            break;
                        case "MINUTE":
                            newExpression = getDiffValue("24*60");
                            break;
                        case "SECOND":
                            newExpression = getDiffValue("24*60*60");
                            break;
                    }
                }
            }

            return newExpression;
        }
    }
}