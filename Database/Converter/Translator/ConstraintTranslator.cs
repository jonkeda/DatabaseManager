using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Databases.Config;
using Databases.Converter.Helper;
using Databases.Interpreter;
using Databases.Model.DatabaseObject;
using Databases.Model.DataType;
using Databases.Model.Enum;

namespace Databases.Converter.Translator
{
    public class ConstraintTranslator : DbObjectTokenTranslator
    {
        private readonly List<TableConstraint> constraints;
        private IEnumerable<DataTypeSpecification> sourceDataTypeSpecifications;

        public ConstraintTranslator(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter,
            List<TableConstraint> constraints) : base(sourceDbInterpreter, targetDbInterpreter)
        {
            this.constraints = constraints;
        }

        internal List<TableColumn> TableColumns { get; set; }

        public override void Translate()
        {
            if (sourceDbInterpreter.DatabaseType == targetDbInterpreter.DatabaseType)
            {
                return;
            }

            if (hasError)
            {
                return;
            }

            FeedbackInfo("Begin to translate constraints.");

            LoadMappings();

            var invalidConstraints = new List<TableConstraint>();

            foreach (var constraint in constraints)
            {
                constraint.Definition = ParseDefinition(constraint.Definition);

                if (targetDbInterpreter.DatabaseType == DatabaseType.Oracle ||
                    targetDbInterpreter.DatabaseType == DatabaseType.Postgres)
                {
                    if (targetDbInterpreter.DatabaseType == DatabaseType.Oracle)
                    {
                        if (constraint.Definition.Contains("SYSDATE"))
                        {
                            invalidConstraints.Add(constraint);
                            continue;
                        }
                    }

                    var likeExp =
                        @"(([\w\[\]""`]+)[\s]+(like)[\s]+(['][\[].+[\]][']))"; //example: ([SHELF] like '[A-Za-z]' OR "SHELF"='N/A'), to match: [SHELF] like '[A-Za-z]'

                    var matches = Regex.Matches(constraint.Definition, likeExp, RegexOptions.IgnoreCase);

                    if (matches.Count > 0)
                    {
                        foreach (Match m in matches)
                        {
                            var items = m.Value.Split(' ');

                            string newValue = null;

                            if (targetDbInterpreter.DatabaseType == DatabaseType.Oracle ||
                                targetDbInterpreter.DatabaseType == DatabaseType.MySql)
                            {
                                newValue = $"REGEXP_LIKE({items[0]},{items[2]})";
                            }
                            else if (targetDbInterpreter.DatabaseType == DatabaseType.Postgres)
                            {
                                newValue = $"{items[0]} similar to ('({items[2].Trim('\'')})')";
                            }

                            if (!string.IsNullOrEmpty(newValue))
                            {
                                constraint.Definition = constraint.Definition.Replace(m.Value, newValue);
                            }
                        }
                    }

                    if (targetDbInterpreter.DatabaseType == DatabaseType.Postgres)
                    {
                        if (TableColumns != null)
                        {
                            var isMoneyConstraint = TableColumns.Any(item =>
                                item.TableName == constraint.TableName &&
                                item.DataType == "money" && constraint.Definition.Contains(item.Name)
                            );

                            if (isMoneyConstraint && !constraint.Definition.ToLower().Contains("::money"))
                            {
                                constraint.Definition =
                                    TranslateHelper.ConvertNumberToPostgresMoney(constraint.Definition);
                            }
                        }
                    }
                }

                if (sourceDbInterpreter.DatabaseType == DatabaseType.Oracle ||
                    sourceDbInterpreter.DatabaseType == DatabaseType.MySql)
                {
                    var likeFunctionName = "REGEXP_LIKE";
                    var likeFunctionNameExp = new Regex($"({likeFunctionName})", RegexOptions.IgnoreCase);

                    if (constraint.Definition.IndexOf(likeFunctionName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var likeExp =
                            $@"({likeFunctionName})[\s]?[(][\w\[\]""` ]+[,][\s]?(['][\[].+[\]]['])[)]"; //example: REGEXP_LIKE("SHELF",'[A-Za-z]')

                        var matches = Regex.Matches(constraint.Definition, likeExp, RegexOptions.IgnoreCase);

                        if (matches.Count > 0)
                        {
                            foreach (Match m in matches)
                            {
                                var items = likeFunctionNameExp.Replace(m.Value, "").Trim('(', ')').Split(',');

                                string newValue = null;

                                if (targetDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                                {
                                    newValue = $"{items[0]} like {items[1]}";
                                }
                                else if (targetDbInterpreter.DatabaseType == DatabaseType.Postgres)
                                {
                                    newValue = $"{items[0]} similar to ('({items[1].Trim('\'')})')";
                                }

                                if (!string.IsNullOrEmpty(newValue))
                                {
                                    constraint.Definition = constraint.Definition.Replace(m.Value, newValue);
                                }
                            }
                        }
                    }
                }
                else if (sourceDbInterpreter.DatabaseType == DatabaseType.Postgres)
                {
                    constraint.Definition = constraint.Definition.Replace("NOT VALID", "");

                    if (constraint.Definition.Contains("::")) //datatype convert operator
                    {
                        LoadSourceDataTypeSpecifications();

                        constraint.Definition = TranslateHelper.RemovePostgresDataTypeConvertExpression(
                            constraint.Definition, sourceDataTypeSpecifications, targetDbInterpreter.QuotationLeftChar,
                            targetDbInterpreter.QuotationRightChar);
                    }

                    if (constraint.Definition.Contains("similar_to_escape"))
                    {
                        //example:  ((((([Shelf]) ~ similar_to_escape('(A-Za-z)')) OR (([Shelf]) = 'N/A')))),
                        //to match (([Shelf]) ~ similar_to_escape('(A-Za-z)'))
                        var likeExp =
                            $@"(([(][\w\{targetDbInterpreter.QuotationLeftChar}\{targetDbInterpreter.QuotationRightChar}]+[)])[\s][~][\s](similar_to_escape)([(]['][(].+[)]['][)]))";

                        var matches = Regex.Matches(constraint.Definition, likeExp, RegexOptions.IgnoreCase);

                        if (matches.Count > 0)
                        {
                            foreach (Match m in matches)
                            {
                                var items = m.Value.Split('~');

                                var columnName = items[0].Trim(' ', '(', ')');
                                var expression =
                                    $"'{items[1].Replace("similar_to_escape", "").Trim(' ', '(', ')', '\'')}'";
                                ;

                                string newValue = null;

                                if (targetDbInterpreter.DatabaseType == DatabaseType.Oracle ||
                                    targetDbInterpreter.DatabaseType == DatabaseType.MySql)
                                {
                                    newValue = $"REGEXP_LIKE({columnName},{expression})";
                                }
                                else if (targetDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                                {
                                    newValue = $"{columnName} like {expression}";
                                }

                                if (!string.IsNullOrEmpty(newValue))
                                {
                                    constraint.Definition = constraint.Definition.Replace(m.Value, newValue);
                                }
                            }
                        }
                    }
                }
            }

            constraints.RemoveAll(item => invalidConstraints.Contains(item));

            FeedbackInfo("End translate constraints.");
        }

        private void LoadSourceDataTypeSpecifications()
        {
            if (sourceDataTypeSpecifications == null)
            {
                sourceDataTypeSpecifications =
                    DataTypeManager.GetDataTypeSpecifications(sourceDbInterpreter.DatabaseType);
            }
        }
    }
}