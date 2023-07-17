using DatabaseConverter.Core.Model.Functions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Model;

namespace DatabaseConverter.Core.Functions
{
    public class DateExtractTranslator : SpecificFunctionTranslatorBase
    {
        public DateExtractTranslator(FunctionSpecification sourceSpecification,
            FunctionSpecification targetSpecification) : base(sourceSpecification, targetSpecification)
        {
        }


        public override string Translate(FunctionFormula formula)
        {
            var expression = formula.Expression;
            var delimiter = SourceSpecification.Delimiter ?? ",";
            var args = formula.GetArgs(delimiter);

            var dateExtract = default(DateExtract?);

            var newExpression = expression;

            if (SourceDbType == DatabaseType.SqlServer)
            {
                dateExtract = new DateExtract { Unit = args[0], Date = args[1] };
            }
            else if (SourceDbType == DatabaseType.MySql)
            {
                var items = args[1].Split(' ');

                dateExtract = new DateExtract { Unit = items[0].Trim(), Date = args[0] };
            }

            if (dateExtract.HasValue)
            {
                var date = dateExtract.Value.Date;
                var format = DatetimeHelper.GetSqliteStrfTimeFormat(SourceDbType, dateExtract.Value.Unit);

                if (TargetDbType == DatabaseType.Sqlite) newExpression = $"STRFTIME('%{format}',{date})";
            }

            return newExpression;
        }
    }
}