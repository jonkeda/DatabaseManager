using DatabaseConverter.Core.Model.Functions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core.Functions
{
    public class DateAddTranslator : SpecificFunctionTranslatorBase
    {
        public DateAddTranslator(FunctionSpecification sourceSpecification, FunctionSpecification targetSpecification) :
            base(sourceSpecification, targetSpecification)
        { }


        public override string Translate(FunctionFormula formula)
        {
            var expression = formula.Expression;
            var delimiter = SourceSpecification.Delimiter ?? ",";
            var args = formula.GetArgs(delimiter);

            var dateAdd = default(DateAdd?);

            var newExpression = expression;

            if (SourceDbType == DatabaseType.SqlServer)
            {
                dateAdd = new DateAdd { Unit = args[0], Date = args[2], IntervalNumber = args[1] };
            }
            else if (SourceDbType == DatabaseType.MySql)
            {
                var items = args[1].Split(' ');

                dateAdd = new DateAdd { Unit = items[2].Trim(), Date = args[0], IntervalNumber = items[1].Trim() };
            }

            if (dateAdd.HasValue)
            {
                var unit = DatetimeHelper.GetMappedUnit(SourceDbType, TargetDbType, dateAdd.Value.Unit);

                var isStringValue = ValueHelper.IsStringValue(dateAdd.Value.Date);
                var date = dateAdd.Value.Date;
                var intervalNumber = dateAdd.Value.IntervalNumber;
                var isTimestampStr = isStringValue && date.Contains(" ");

                if (TargetDbType == DatabaseType.SqlServer)
                {
                    newExpression = $"DATEADD({unit}, {intervalNumber},{date})";
                }
                else if (TargetDbType == DatabaseType.MySql)
                {
                    newExpression = $"DATE_ADD({date},INTERVAL {intervalNumber} {unit})";
                }
                else if (TargetDbType == DatabaseType.Postgres)
                {
                    var dataType = isStringValue ? isTimestampStr ? "::TIMESTAMP" : "::DATE" : "";

                    var strDate = $"{date}{dataType}";
                    ;

                    newExpression = $"{strDate}+ INTERVAL '{intervalNumber} {unit}'";
                }
                else if (TargetDbType == DatabaseType.Oracle)
                {
                    var isDateStr = isStringValue && !date.Contains(" ");

                    if (isStringValue)
                    {
                        date = DatetimeHelper.GetOracleUniformDatetimeString(date, isTimestampStr);
                    }

                    var dataType = isStringValue ? isTimestampStr ? "TIMESTAMP" : "DATE" : "";

                    var strDate = $"{dataType}{date}";

                    newExpression = $"{strDate} + INTERVAL '{intervalNumber}' {unit}";
                }
                else if (TargetDbType == DatabaseType.Sqlite)
                {
                    if (unit == "WEEK")
                    {
                        intervalNumber = intervalNumber.StartsWith("-") ? "-7" : "7";
                        unit = "DAY";
                    }

                    var function = isStringValue ? isTimestampStr ? "DATETIME" : "DATE" : "";

                    newExpression = $"{function}({date}, '{intervalNumber} {unit}')";
                }
            }

            return newExpression;
        }
    }
}