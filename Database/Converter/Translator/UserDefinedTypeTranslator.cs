using System.Collections.Generic;
using Databases.Interpreter;
using Databases.Model.DatabaseObject;
using Databases.Model.DataType;

namespace Databases.Converter.Translator
{
    public class UserDefinedTypeTranslator : DbObjectTranslator
    {
        private readonly DataTypeTranslator dataTypeTranslator;
        private readonly IEnumerable<UserDefinedType> userDefinedTypes;

        public UserDefinedTypeTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            IEnumerable<UserDefinedType> userDefinedTypes) : base(sourceInterpreter, targetInterpreter)
        {
            this.userDefinedTypes = userDefinedTypes;
            dataTypeTranslator = new DataTypeTranslator(sourceDbInterpreter, targetDbInterpreter);
        }

        public override void Translate()
        {
            if (sourceDbType == targetDbType)
            {
                return;
            }

            FeedbackInfo("Begin to translate user defined types.");

            foreach (var udt in userDefinedTypes)
            foreach (var attr in udt.Attributes)
            {
                var dataTypeInfo = new DataTypeInfo
                {
                    DataType = attr.DataType,
                    MaxLength = attr.MaxLength,
                    Precision = attr.Precision,
                    Scale = attr.Scale
                };

                dataTypeTranslator.Translate(dataTypeInfo);

                attr.DataType = dataTypeInfo.DataType;
                attr.MaxLength = dataTypeInfo.MaxLength;
                attr.Precision = dataTypeInfo.Precision;
                attr.Scale = dataTypeInfo.Scale;
            }

            FeedbackInfo("End translate user defined types.");
        }
    }
}