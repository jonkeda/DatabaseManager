using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace Databases.SqlAnalyser
{
    public class StatementScriptBuilderHelper
    {
        public static TableName GetSelectStatementTableName(SelectStatement statement)
        {
            if (statement.TableName != null)
            {
                return statement.TableName;
            }

            if (statement.HasFromItems)
            {
                return statement.FromItems.FirstOrDefault()?.TableName;
            }

            return null;
        }

        public static TableName GetUpdateSetTableName(UpdateStatement statement)
        {
            TableName tableName = null;

            var tableNames = statement.TableNames;
            var fromItems = statement.FromItems;
            var setItems = statement.SetItems;

            var setNames = statement.SetItems.Select(item => item.Name);

            var tableNameOrAliases = new List<string>();

            foreach (var setName in setItems)
            {
                if (setName.Name.Symbol.Contains("."))
                {
                    tableNameOrAliases.Add(setName.Name.Symbol.Split('.')[0].Trim().ToLower());
                }
            }

            if (fromItems != null)
            {
                tableName = fromItems.FirstOrDefault(item => item.TableName != null &&
                                                             (tableNameOrAliases.Contains(
                                                                  item.TableName.Symbol.ToLower())
                                                              || item.TableName.Alias != null &&
                                                                  tableNameOrAliases.Contains(item.TableName.Alias
                                                                      .Symbol.ToLower())))?.TableName;
            }

            if (tableName == null && tableNames != null)
            {
                tableName = tableNames.FirstOrDefault(item => tableNameOrAliases.Contains(item.Symbol.ToLower())
                                                              || item.Alias != null &&
                                                                  tableNameOrAliases.Contains(
                                                                      item.Alias.Symbol.ToLower()));
            }

            return tableName;
        }

        /// <summary>
        ///     oracle:update table set (col1,col2)=(select col1,col2 from ...)
        /// </summary>
        /// <param name="statement"></param>
        public static bool IsCompositeUpdateSetColumnName(UpdateStatement statement)
        {
            return statement.SetItems.Any(item => item.Name?.Symbol?.Contains(",") == true);
        }

        public static string ParseCompositeUpdateSet(DatabaseType builder, UpdateStatement statement)
        {
            var sb = new StringBuilder();

            var setItem = statement.SetItems.FirstOrDefault();
            var valueStatement = setItem.ValueStatement;

            if (valueStatement == null)
            {
                return string.Empty;
            }

            var where = valueStatement.Where;

            var colNames = statement.SetItems.FirstOrDefault().Name.Symbol.Trim('(', ')').Split(',');

            var colValues = valueStatement.Columns.Select(item => item.Symbol).ToArray();

            void BuildSet()
            {
                for (var i = 0; i < colNames.Length; i++)
                {
                    sb.Append($"{colNames[i].Trim()}={colValues[i].Trim()}{(i < colNames.Length - 1 ? ", " : "")}");

                    if (i == colNames.Length - 1)
                    {
                        sb.AppendLine();
                    }
                }
            }

            string GetFromTables()
            {
                if (valueStatement.HasFromItems)
                {
                    return string.Join(",", valueStatement.FromItems.Select(item => item.TableName.NameWithAlias));
                }

                return string.Empty;
            }

            void BuildWhere()
            {
                if (where != null)
                {
                    sb.AppendLine($"WHERE {where.Symbol}");
                }
            }

            void BuildFromAndWhere()
            {
                var fromTables = GetFromTables();

                sb.AppendLine($"FROM {fromTables}");

                BuildWhere();
            }

            if (builder == DatabaseType.SqlServer || builder == DatabaseType.Sqlite)
            {
                BuildSet();

                BuildFromAndWhere();
            }
            else if (builder == DatabaseType.MySql)
            {
                var fromTables = GetFromTables();

                sb.AppendLine(fromTables);

                sb.AppendLine("SET");

                BuildSet();

                BuildWhere();
            }
            else if (builder == DatabaseType.Postgres)
            {
                for (var i = 0; i < colNames.Length; i++)
                {
                    if (colNames[i].Contains("."))
                    {
                        colNames[i] = colNames[i].Split('.').Last().Trim();
                    }
                }

                BuildSet();

                if (valueStatement.HasFromItems)
                {
                    var fromTables = GetFromTables();

                    var tableName = GetUpdateSetTableName(statement);

                    var strTableName = tableName?.Symbol;
                    var alias = tableName?.Alias?.Symbol;

                    var fromTableList = new List<string>();

                    foreach (var fromTable in fromTables.Split(','))
                    {
                        var items = fromTable.Split(' ').Select(item => item.Trim());

                        if (!items.Any(item => item == strTableName || item == alias))
                        {
                            fromTableList.Add(fromTable);
                        }
                    }

                    sb.AppendLine($"FROM {string.Join(",", fromTableList)}");
                }

                BuildWhere();
            }

            return sb.ToString();
        }

        public static string ConvertToSelectIntoVariable(string name, string value)
        {
            value = value.Trim();

            if (value.StartsWith("(") && value.EndsWith(")"))
            {
                value = StringHelper.GetBalanceParenthesisTrimedValue(value);
            }

            var fromIndex = value.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);

            if (fromIndex < 0)
            {
                return $"{name}:={value};";
            }

            return $"{value.Substring(0, fromIndex)} INTO {name} {value.Substring(fromIndex)};";
        }
    }
}