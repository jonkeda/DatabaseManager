using System;
using System.Collections.Generic;
using DatabaseConverter.Core.Model;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public class TranslateEngine
    {
        public const DatabaseObjectType SupportDatabaseObjectType =
            DatabaseObjectType.Column | DatabaseObjectType.Constraint |
            DatabaseObjectType.View | DatabaseObjectType.Function | DatabaseObjectType.Procedure |
            DatabaseObjectType.Trigger | DatabaseObjectType.Sequence | DatabaseObjectType.Type;

        private IObserver<FeedbackInfo> observer;
        private readonly DbConverterOption option;
        private readonly DbInterpreter sourceInterpreter;
        private readonly SchemaInfo sourceSchemaInfo;
        private readonly DbInterpreter targetInterpreter;
        private readonly SchemaInfo targetSchemaInfo;

        public TranslateEngine(SchemaInfo sourceSchemaInfo, SchemaInfo targetSchemaInfo,
            DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter, DbConverterOption option = null)
        {
            this.sourceSchemaInfo = sourceSchemaInfo;
            this.targetSchemaInfo = targetSchemaInfo;
            this.sourceInterpreter = sourceInterpreter;
            this.targetInterpreter = targetInterpreter;
            this.option = option;
        }

        public List<TableColumn> ExistedTableColumns { get; internal set; }
        public List<UserDefinedType> UserDefinedTypes { get; internal set; } = new List<UserDefinedType>();

        public List<TranslateResult> TranslateResults { get; } = new List<TranslateResult>();

        public bool ContinueWhenErrorOccurs { get; set; }

        public void Translate(DatabaseObjectType databaseObjectType = DatabaseObjectType.None)
        {
            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Type))
            {
                var userDefinedTypeTranslator = new UserDefinedTypeTranslator(sourceInterpreter, targetInterpreter,
                    targetSchemaInfo.UserDefinedTypes);
                Translate(userDefinedTypeTranslator);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Sequence))
            {
                var sequenceTranslator =
                    new SequenceTranslator(sourceInterpreter, targetInterpreter, targetSchemaInfo.Sequences);
                Translate(sequenceTranslator);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Column))
            {
                var columnTranslator =
                    new ColumnTranslator(sourceInterpreter, targetInterpreter, targetSchemaInfo.TableColumns);
                columnTranslator.Option = option;
                columnTranslator.UserDefinedTypes = UserDefinedTypes;
                columnTranslator.ExistedTableColumns = ExistedTableColumns;
                Translate(columnTranslator);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Constraint))
            {
                var constraintTranslator =
                    new ConstraintTranslator(sourceInterpreter, targetInterpreter, targetSchemaInfo.TableConstraints)
                        { ContinueWhenErrorOccurs = ContinueWhenErrorOccurs };
                constraintTranslator.TableColumns = targetSchemaInfo.TableColumns;
                Translate(constraintTranslator);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.View))
            {
                var viewTranslator = GetScriptTranslator(targetSchemaInfo.Views);
                viewTranslator.Translate();

                TranslateResults.AddRange(viewTranslator.TranslateResults);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Function))
            {
                var functionTranslator = GetScriptTranslator(targetSchemaInfo.Functions);
                functionTranslator.Translate();

                TranslateResults.AddRange(functionTranslator.TranslateResults);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Procedure))
            {
                var procedureTranslator = GetScriptTranslator(targetSchemaInfo.Procedures);
                procedureTranslator.Translate();

                TranslateResults.AddRange(procedureTranslator.TranslateResults);
            }

            if (NeedTranslate(databaseObjectType, DatabaseObjectType.Trigger))
            {
                var triggerTranslator = GetScriptTranslator(targetSchemaInfo.TableTriggers);
                triggerTranslator.Translate();

                TranslateResults.AddRange(triggerTranslator.TranslateResults);
            }
        }

        private void Translate(DbObjectTranslator translator)
        {
            translator.Option = option;
            translator.SourceSchemaInfo = sourceSchemaInfo;
            translator.Subscribe(observer);
            translator.Translate();
        }

        private bool NeedTranslate(DatabaseObjectType databaseObjectType, DatabaseObjectType currentDbType)
        {
            if (databaseObjectType == DatabaseObjectType.None || databaseObjectType.HasFlag(currentDbType)) return true;

            return false;
        }

        private ScriptTranslator<T> GetScriptTranslator<T>(List<T> dbObjects) where T : ScriptDbObject
        {
            var translator = new ScriptTranslator<T>(sourceInterpreter, targetInterpreter, dbObjects)
                { ContinueWhenErrorOccurs = ContinueWhenErrorOccurs };
            translator.Option = option;
            translator.UserDefinedTypes = UserDefinedTypes;
            translator.Subscribe(observer);

            return translator;
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }
    }
}