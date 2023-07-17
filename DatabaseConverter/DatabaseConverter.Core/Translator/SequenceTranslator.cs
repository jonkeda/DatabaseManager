using System.Collections.Generic;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public class SequenceTranslator : DbObjectTranslator
    {
        public const string SqlServerSequenceNextValueFlag = "NEXT VALUE FOR";
        public const string PostgreSeqenceNextValueFlag = "NEXTVAL";
        public const string OracleSequenceNextValueFlag = "NEXTVAL";
        private readonly IEnumerable<Sequence> sequences;

        public SequenceTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter) : base(
            sourceInterpreter, targetInterpreter)
        {
        }

        public SequenceTranslator(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter,
            IEnumerable<Sequence> sequences) : base(sourceInterpreter, targetInterpreter)
        {
            this.sequences = sequences;
        }

        public override void Translate()
        {
            if (sourceDbType == targetDbType) return;

            FeedbackInfo("Begin to translate sequences.");

            foreach (var sequence in sequences)
            {
                ConvertDataType(sequence);

                if (sequence.StartValue < sequence.MinValue) sequence.StartValue = (int)sequence.MinValue;
            }

            FeedbackInfo("End translate sequences.");
        }

        public void ConvertDataType(Sequence sequence)
        {
            if (targetDbType == DatabaseType.SqlServer) sequence.DataType = "bigint";
        }

        public static bool IsSequenceValueFlag(DatabaseType databaseType, string value)
        {
            var upperValue = value.ToUpper();

            if (databaseType == DatabaseType.SqlServer)
                return upperValue.Contains(SqlServerSequenceNextValueFlag);
            if (databaseType == DatabaseType.Postgres)
                return upperValue.Contains(PostgreSeqenceNextValueFlag);
            if (databaseType == DatabaseType.Oracle) return upperValue.Contains(OracleSequenceNextValueFlag);

            return false;
        }

        public string HandleSequenceValue(string value)
        {
            var nextValueFlag = "";
            string sequencePart;

            if (sourceDbType == DatabaseType.SqlServer)
            {
                nextValueFlag = SqlServerSequenceNextValueFlag;
            }
            else if (sourceDbType == DatabaseType.Postgres)
            {
                nextValueFlag = PostgreSeqenceNextValueFlag;
                value = value.Replace("::regclass", "");
            }
            else if (sourceDbType == DatabaseType.Oracle)
            {
                nextValueFlag = OracleSequenceNextValueFlag;
            }

            sequencePart = StringHelper
                .GetBalanceParenthesisTrimedValue(value.ReplaceOrdinalIgnoreCase(nextValueFlag, "").Trim()).Trim('\'');

            string schema = null, sequenceName;

            if (sequencePart.Contains("."))
            {
                var items = sequencePart.Split('.');
                schema = GetTrimmedName(items[0]);
                sequenceName = GetTrimmedName(items[1]);
            }
            else
            {
                sequenceName = GetTrimmedName(sequencePart);
            }

            var mappedSchema = GetMappedSchema(schema);

            return ConvertSequenceValue(targetDbInterpreter, mappedSchema, sequenceName);
        }

        public static string ConvertSequenceValue(DbInterpreter targetDbInterpreter, string schema, string sequenceName)
        {
            var targetDbType = targetDbInterpreter.DatabaseType;

            if (targetDbType == DatabaseType.SqlServer)
                return
                    $"{SqlServerSequenceNextValueFlag} {targetDbInterpreter.GetQuotedDbObjectNameWithSchema(schema, sequenceName)}";
            if (targetDbType == DatabaseType.Postgres)
                return
                    $"{PostgreSeqenceNextValueFlag}('{targetDbInterpreter.GetQuotedDbObjectNameWithSchema(schema, sequenceName)}')";
            if (targetDbType == DatabaseType.Oracle)
                return
                    $"{targetDbInterpreter.GetQuotedDbObjectNameWithSchema(schema, sequenceName)}.{OracleSequenceNextValueFlag}";

            return targetDbInterpreter.GetQuotedDbObjectNameWithSchema(schema, sequenceName);
        }

        private string GetMappedSchema(string schema)
        {
            var mappedSchema = SchemaInfoHelper.GetMappedSchema(GetTrimmedName(schema), Option.SchemaMappings);

            if (mappedSchema == null) mappedSchema = targetDbInterpreter.DefaultSchema;

            return mappedSchema;
        }
    }
}