using Databases.Converter.Model;
using Databases.Model.Enum;
using Databases.Model.Function;

namespace Databases.Converter.Translator.Functions
{
    public abstract class SpecificFunctionTranslatorBase
    {
        protected FunctionSpecification SourceSpecification;
        protected FunctionSpecification TargetSpecification;

        public SpecificFunctionTranslatorBase(FunctionSpecification sourceSpecification,
            FunctionSpecification targetSpecification)
        {
            SourceSpecification = sourceSpecification;
            TargetSpecification = targetSpecification;
        }

        public DatabaseType SourceDbType { get; set; }
        public DatabaseType TargetDbType { get; set; }

        public abstract string Translate(FunctionFormula formula);
    }
}