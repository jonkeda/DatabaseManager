using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseConverter.Core
{
    public class ViewTranslator : DbObjectTokenTranslator
    {
        private string targetSchemaName;
        private readonly List<View> views;

        public ViewTranslator(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter, List<View> views,
            string targetSchemaName = null) : base(sourceDbInterpreter, targetDbInterpreter)
        {
            this.views = views;
            this.targetSchemaName = targetSchemaName;
        }

        public override void Translate()
        {
            //if (sourceDbInterpreter.DatabaseType == targetDbInterpreter.DatabaseType) return;

            if (hasError) return;

            LoadMappings();

            if (string.IsNullOrEmpty(targetSchemaName))
            {
                if (targetDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                    targetSchemaName = "dbo";
                else
                    targetSchemaName = targetDbInterpreter.DefaultSchema;
            }

            foreach (var view in views)
                try
                {
                    var viewNameWithQuotation =
                        $"{targetDbInterpreter.QuotationLeftChar}{view.Name}{targetDbInterpreter.QuotationRightChar}";

                    var definition = view.Definition;

                    definition = definition
                        .Replace(sourceDbInterpreter.QuotationLeftChar, '"')
                        .Replace(sourceDbInterpreter.QuotationRightChar, '"')
                        .Replace("<>", "!=")
                        .Replace(">", " > ")
                        .Replace("<", " < ")
                        .Replace("!=", "<>");

                    var sb = new StringBuilder();

                    var lines = definition.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        if (line.StartsWith(sourceDbInterpreter.CommentString)) continue;

                        sb.AppendLine(line);
                    }

                    definition = ParseDefinition(sb.ToString());

                    var createClause = targetDbInterpreter.DatabaseType == DatabaseType.Oracle
                        ? "CREATE OR REPLACE"
                        : "CREATE";

                    var createAsClause =
                        $"{createClause} VIEW {(string.IsNullOrEmpty(targetSchemaName) ? "" : targetSchemaName + ".")}{viewNameWithQuotation} AS ";

                    if (!definition.Trim().ToLower().StartsWith("create"))
                    {
                        definition = createAsClause + Environment.NewLine + definition;
                    }
                    else
                    {
                        var asIndex = definition.ToLower().IndexOf("as", StringComparison.Ordinal);
                        definition = createAsClause + definition.Substring(asIndex + 2);
                    }

                    view.Definition = definition;

                    if (Option.CollectTranslateResultAfterTranslated)
                        TranslateResults.Add(new TranslateResult
                        {
                            DbObjectType = DatabaseObjectType.View, DbObjectName = view.Name, Data = view.Definition
                        });
                }
                catch (Exception ex)
                {
                    var vce = new ViewConvertException(ex)
                    {
                        SourceServer = sourceDbInterpreter.ConnectionInfo.Server,
                        SourceDatabase = sourceDbInterpreter.ConnectionInfo.Database,
                        SourceObject = view.Name,
                        TargetServer = targetDbInterpreter.ConnectionInfo.Server,
                        TargetDatabase = targetDbInterpreter.ConnectionInfo.Database,
                        TargetObject = view.Name
                    };

                    if (!ContinueWhenErrorOccurs)
                        throw vce;
                    FeedbackError(ExceptionHelper.GetExceptionDetails(ex), ContinueWhenErrorOccurs);
                }
        }

        public override string ParseDefinition(string definition)
        {
            definition = base.ParseDefinition(definition);

            #region Handle join cluase for mysql which has no "on", so it needs to make up that.

            try
            {
                var sb = new StringBuilder();

                if (sourceDbInterpreter.DatabaseType == DatabaseType.MySql)
                {
                    var hasError = false;
                    var formattedDefinition = FormatSql(definition, out hasError);

                    if (!hasError)
                    {
                        var lines = formattedDefinition.Split(new[] { '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries);

                        var joinRegex = new Regex(@"\b(join)\b", RegexOptions.IgnoreCase);
                        var onRegex = new Regex(@"\b(on)\b", RegexOptions.IgnoreCase);
                        var wordRegex = new Regex("([a-zA-Z(]+)", RegexOptions.IgnoreCase);

                        sb = new StringBuilder();
                        foreach (var line in lines)
                        {
                            var hasChanged = false;

                            if (joinRegex.IsMatch(line))
                            {
                                var leftStr = line.Substring(line.ToLower().LastIndexOf("join") + 4);

                                if (!onRegex.IsMatch(line) && !wordRegex.IsMatch(leftStr))
                                {
                                    hasChanged = true;
                                    sb.AppendLine($"{line} ON 1=1 ");
                                }
                            }

                            if (!hasChanged) sb.AppendLine(line);
                        }

                        definition = sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                var info = new FeedbackInfo
                {
                    InfoType = FeedbackInfoType.Error, Message = ExceptionHelper.GetExceptionDetails(ex), Owner = this
                };
                FeedbackHelper.Feedback(info);
            }

            #endregion

            return definition.Trim();
        }
    }
}