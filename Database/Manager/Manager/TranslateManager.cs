using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Databases.Converter;
using Databases.Converter.Helper;
using Databases.Converter.Model;
using Databases.Converter.Translator;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Model;
using Databases.Manager.Script;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Enum;
using Databases.Model.Option;

namespace Databases.Manager.Manager
{
    public class TranslateManager
    {
        private IObserver<FeedbackInfo> observer;

        public async Task<TranslateResult> Translate(DatabaseType sourceDbType, DatabaseType targetDbType,
            DatabaseObject dbObject, ConnectionInfo connectionInfo, bool removeCarriagRreturnChar = false)
        {
            var sourceScriptOption = new DbInterpreterOption
                { ScriptOutputMode = GenerateScriptOutputMode.None, ObjectFetchMode = DatabaseObjectFetchMode.Details };
            var targetScriptOption = new DbInterpreterOption
                { ScriptOutputMode = GenerateScriptOutputMode.WriteToString };

            var source = new DbConverterInfo
            {
                DbInterpreter = DbInterpreterHelper.GetDbInterpreter(sourceDbType, connectionInfo, sourceScriptOption)
            };
            var target = new DbConverterInfo
            {
                DbInterpreter =
                    DbInterpreterHelper.GetDbInterpreter(targetDbType, new ConnectionInfo(), targetScriptOption)
            };

            using (var dbConverter = new DbConverter(source, target))
            {
                var option = dbConverter.Option;

                option.OnlyForTranslate = true;
                option.GenerateScriptMode = GenerateScriptMode.Schema;
                option.ExecuteScriptOnTargetServer = false;
                option.ConvertComputeColumnExpression = true;
                option.UseOriginalDataTypeIfUdtHasOnlyOneAttr =
                    SettingManager.Setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr;
                option.RemoveCarriagRreturnChar = removeCarriagRreturnChar;
                option.ConvertConcatChar =
                    TranslateHelper.NeedConvertConcatChar(SettingManager.Setting.ConvertConcatCharTargetDatabases,
                        targetDbType);

                dbConverter.Subscribe(observer);

                var result = await dbConverter.Translate(dbObject);

                return result.TranslateResults.FirstOrDefault();
            }
        }

        public TranslateResult Translate(DatabaseType sourceDbType, DatabaseType targetDbType, string script)
        {
            var result = new TranslateResult();

            var sourceDbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(sourceDbType, new ConnectionInfo(), new DbInterpreterOption());
            var targetDbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(targetDbType, new ConnectionInfo(), new DbInterpreterOption());

            dynamic scriptDbObjects;

            dynamic translator;

            var scriptType = ScriptParser.DetectScriptType(script, sourceDbInterpreter);

            if (scriptType == ScriptType.View)
            {
                scriptDbObjects = ConvertScriptDbObject<View>(script);
                translator = this.GetScriptTranslator<View>(scriptDbObjects, sourceDbInterpreter, targetDbInterpreter);
            }
            else if (scriptType == ScriptType.Function)
            {
                scriptDbObjects = ConvertScriptDbObject<Function>(script);
                translator =
                    this.GetScriptTranslator<Function>(scriptDbObjects, sourceDbInterpreter, targetDbInterpreter);
            }
            else if (scriptType == ScriptType.Procedure)
            {
                scriptDbObjects = ConvertScriptDbObject<Procedure>(script);
                translator =
                    this.GetScriptTranslator<Procedure>(scriptDbObjects, sourceDbInterpreter, targetDbInterpreter);
            }
            else if (scriptType == ScriptType.Trigger)
            {
                scriptDbObjects = ConvertScriptDbObject<TableTrigger>(script);
                translator =
                    this.GetScriptTranslator<TableTrigger>(scriptDbObjects, sourceDbInterpreter, targetDbInterpreter);
            }
            else
            {
                scriptDbObjects = new List<ScriptDbObject> { new ScriptDbObject { Definition = script } };

                translator =
                    this.GetScriptTranslator<ScriptDbObject>(scriptDbObjects, sourceDbInterpreter, targetDbInterpreter);

                translator.IsCommonScript = true;
            }

            //use default schema
            if (scriptDbObjects != null)
            {
                foreach (var sdo in scriptDbObjects)
                {
                    sdo.Schema = targetDbInterpreter.DefaultSchema;
                }
            }

            if (translator != null)
            {
                translator.Translate();

                List<TranslateResult> results = translator.TranslateResults;

                result = results.FirstOrDefault();
            }

            return result;
        }

        private IEnumerable<T> ConvertScriptDbObject<T>(string script) where T : ScriptDbObject, new()
        {
            var list = new List<T>();
            var t = new T
            {
                Definition = script
            };

            list.Add(t);

            return list;
        }

        private ScriptTranslator<T> GetScriptTranslator<T>(IEnumerable<T> dbObjects, DbInterpreter sourceDbInterpreter,
            DbInterpreter targetDbInterpreter) where T : ScriptDbObject
        {
            var translator = new ScriptTranslator<T>(sourceDbInterpreter, targetDbInterpreter, dbObjects);
            translator.Option = new DbConverterOption
            {
                OutputRemindInformation = false,
                ConvertConcatChar = TranslateHelper.NeedConvertConcatChar(
                    SettingManager.Setting.ConvertConcatCharTargetDatabases, targetDbInterpreter.DatabaseType)
            };
            translator.AutoMakeupSchemaName = false;

            return translator;
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }
    }
}