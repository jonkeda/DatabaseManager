﻿using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using System;
using System.Collections.Generic;
using System.Reflection;
using SqlAnalyser.Core;
using System.Linq;
using SqlAnalyser.Model;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Utility;
using System.Text;

namespace DatabaseConverter.Core
{
    public class ScriptTranslator<T> : DbObjectTokenTranslator
        where T : ScriptDbObject
    {
        private List<T> scripts;
        private DatabaseType sourceDbType;
        private DatabaseType targetDbType;

        public List<UserDefinedType> UserDefinedTypes { get; set; } = new List<UserDefinedType>();
        public string TargetDbOwner { get; set; }
        

        public ScriptTranslator(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter, List<T> scripts) : base(sourceDbInterpreter, targetDbInterpreter)
        {
            this.sourceDbType = sourceDbInterpreter.DatabaseType;
            this.targetDbType = targetDbInterpreter.DatabaseType;
            this.scripts = scripts;
        }

        public override void Translate()
        {
            if (this.sourceDbInterpreter.DatabaseType == this.targetDbInterpreter.DatabaseType)
            {
                return;
            }

            this.LoadMappings();

            SqlAnalyserBase sourceAnalyser = this.GetSqlAnalyser(this.sourceDbInterpreter.DatabaseType);
            SqlAnalyserBase targetAnalyser = this.GetSqlAnalyser(this.targetDbInterpreter.DatabaseType);           

            Action<T, CommonScript> processTokens = (dbObj, script) =>
            {
                if (typeof(T) == typeof(Function))
                {
                    AnalyseResult result = sourceAnalyser.AnalyseFunction(dbObj.Definition.ToUpper());

                    if(!result.HasError)
                    {
                        RoutineScript routine = result.Script as RoutineScript;

                        if (this.targetDbInterpreter.DatabaseType == DatabaseType.MySql && routine.ReturnTable != null)
                        {
                            routine.Type = RoutineType.PROCEDURE;
                        }
                    }                   
                }

                ScriptTokenProcessor tokenProcessor = new ScriptTokenProcessor(script, dbObj, this.sourceDbInterpreter, this.targetDbInterpreter);
                tokenProcessor.UserDefinedTypes = this.UserDefinedTypes;
                tokenProcessor.TargetDbOwner = this.TargetDbOwner;

                tokenProcessor.Process();

                dbObj.Definition = targetAnalyser.GenerateScripts(script);
            };

            foreach (T dbObj in this.scripts)
            {
                try
                {
                    Type type = typeof(T);

                    bool tokenProcessed = false;

                    this.Validate(dbObj);

                    AnalyseResult result = sourceAnalyser.Analyse<T>(dbObj.Definition.ToUpper());

                    CommonScript script = result.Script;

                    if (result.HasError)
                    {
                        #region Special handle for view
                        if (typeof(T) == typeof(View))
                        {
                            //Currently, ANTLR can't parse some complex tsql accurately, so it uses general strategy.
                            if (this.sourceDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                            {
                                ViewTranslator viewTranslator = new ViewTranslator(this.sourceDbInterpreter, this.targetDbInterpreter, new List<View>() { dbObj as View }, this.TargetDbOwner) { SkipError = this.SkipError };
                                viewTranslator.Translate();
                            }

                            //Currently, ANTLR can't parse some view correctly, use procedure to parse it temporarily.
                            if (this.sourceDbInterpreter.DatabaseType == DatabaseType.Oracle)
                            {
                                string oldDefinition = dbObj.Definition;

                                int asIndex = oldDefinition.ToUpper().IndexOf(" AS ");

                                StringBuilder sbNewDefinition = new StringBuilder();

                                sbNewDefinition.AppendLine($"CREATE OR REPLACE PROCEDURE {dbObj.Name} AS");
                                sbNewDefinition.AppendLine("BEGIN");
                                sbNewDefinition.AppendLine($"{oldDefinition.Substring(asIndex + 5).TrimEnd(';') + ";"}");
                                sbNewDefinition.AppendLine($"END {dbObj.Name};");

                                dbObj.Definition = sbNewDefinition.ToString();

                                AnalyseResult procResult = sourceAnalyser.Analyse<Procedure>(dbObj.Definition.ToUpper());

                                if (!procResult.HasError)
                                {
                                    processTokens(dbObj, procResult.Script);

                                    tokenProcessed = true;

                                    dbObj.Definition = Regex.Replace(dbObj.Definition, " PROCEDURE ", " VIEW ", RegexOptions.IgnoreCase);
                                    dbObj.Definition = Regex.Replace(dbObj.Definition, @"(BEGIN[\r][\n])|(END[\r][\n])", "", RegexOptions.IgnoreCase);                                    
                                }
                            }
                        }
                        #endregion
                    }

                    if (!result.HasError && !tokenProcessed)
                    {
                        processTokens(dbObj, script);
                    }

                    bool formatHasError = false;

                    string definition = this.ReplaceVariables(dbObj.Definition);

                    dbObj.Definition = definition; // this.FormatSql(definition, out formatHasError);

                    if (formatHasError)
                    {
                        dbObj.Definition = definition;
                    }

                    if (this.OnTranslated != null)
                    {
                        this.OnTranslated(this.targetDbInterpreter.DatabaseType, dbObj, dbObj.Definition);
                    }
                }
                catch (Exception ex)
                {
                    var sce = new ScriptConvertException<T>(ex)
                    {
                        SourceServer = this.sourceDbInterpreter.ConnectionInfo.Server,
                        SourceDatabase = this.sourceDbInterpreter.ConnectionInfo.Database,
                        SourceObject = dbObj.Name,
                        TargetServer = this.targetDbInterpreter.ConnectionInfo.Server,
                        TargetDatabase = this.targetDbInterpreter.ConnectionInfo.Database,
                        TargetObject = dbObj.Name
                    };

                    if (!this.SkipError)
                    {
                        throw sce;
                    }
                    else
                    {
                        FeedbackInfo info = new FeedbackInfo() { InfoType = FeedbackInfoType.Error, Message = ExceptionHelper.GetExceptionDetails(ex), Owner = this };
                        FeedbackHelper.Feedback(info);
                    }
                }
            }
        }

        public void Validate(ScriptDbObject script)
        {
            if (sourceDbType == DatabaseType.SqlServer && targetDbType != DatabaseType.SqlServer)
            {
                //ANTRL can't handle "top 100 percent" correctly.
                Regex regex = new Regex(@"(TOP[\s]+100[\s]+PERCENT)", RegexOptions.IgnoreCase);

                if (regex.IsMatch(script.Definition))
                {
                    script.Definition = regex.Replace(script.Definition, "");
                }
            }

            if (script.Owner != this.TargetDbOwner)
            {
                Regex ownerRegex = new Regex($@"[{this.sourceDbInterpreter.QuotationLeftChar}]({script.Owner})[{this.sourceDbInterpreter.QuotationRightChar}][\.]", RegexOptions.IgnoreCase);

                if(ownerRegex.IsMatch(script.Definition))
                {
                    script.Definition = ownerRegex.Replace(script.Definition, "");
                }
            }
        }

        public SqlAnalyserBase GetSqlAnalyser(DatabaseType dbType)
        {
            SqlAnalyserBase sqlAnalyser = null;

            if (dbType == DatabaseType.SqlServer)
            {
                sqlAnalyser = new TSqlAnalyser();
            }
            else if (dbType == DatabaseType.MySql)
            {
                sqlAnalyser = new MySqlAnalyser();
            }
            else if (dbType == DatabaseType.Oracle)
            {
                sqlAnalyser = new PlSqlAnalyser();
            }

            return sqlAnalyser;
        }
    }
}
