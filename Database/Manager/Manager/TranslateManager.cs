using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatabaseConverter.Core;
using DatabaseConverter.Core.Model;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseManager.Core
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

            var source = new DbConveterInfo
            {
                DbInterpreter = DbInterpreterHelper.GetDbInterpreter(sourceDbType, connectionInfo, sourceScriptOption)
            };
            var target = new DbConveterInfo
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
                foreach (var sdo in scriptDbObjects)
                    sdo.Schema = targetDbInterpreter.DefaultSchema;

            if (translator != null)
            {
                translator.Translate();

                List<TranslateResult> results = translator.TranslateResults;

                result = results.FirstOrDefault();
            }

            return result;
        }

        private IEnumerable<T> ConvertScriptDbObject<T>(string script) where T : ScriptDbObject
        {
            var list = new List<T>();

            var t = Activator.CreateInstance(typeof(T)) as T;
            t.Definition = script;

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