using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model;
using Databases.Manager.Model.DbObjectDisplay;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;
using Databases.Model.Schema;
using Databases.Model.Script;
using Databases.ScriptGenerator;

namespace Databases.Manager.Script
{
    public class ScriptGenerator
    {
        private readonly DbInterpreter dbInterpreter;

        public ScriptGenerator(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
        }

        public async Task<ScriptGenerateResult> Generate(DatabaseObject dbObject, ScriptAction scriptAction)
        {
            var result = new ScriptGenerateResult();

            var typeName = dbObject.GetType().Name;

            var databaseObjectType = DbObjectHelper.GetDatabaseObjectType(dbObject);

            var columnType = ColumnType.TableColumn;

            if (dbObject is Table)
            {
                if (scriptAction == ScriptAction.CREATE)
                {
                    dbInterpreter.Option.GetTableAllObjects = true;
                }
                else
                {
                    databaseObjectType |= DatabaseObjectType.Column;
                }
            }
            else if (dbObject is View)
            {
                if (scriptAction == ScriptAction.SELECT)
                {
                    databaseObjectType |= DatabaseObjectType.Column;

                    columnType = ColumnType.ViewColumn;
                }
            }

            var filter = new SchemaInfoFilter
            {
                DatabaseObjectType = databaseObjectType, ColumnType = columnType,
                Schema = dbObject.Schema
            };

            if (columnType == ColumnType.ViewColumn)
            {
                filter.TableNames = new[] { dbObject.Name };
            }
            else
            {
                filter.GetType().GetProperty($"{typeName}Names").SetValue(filter, new[] { dbObject.Name });
            }

            if (dbObject is Function func)
            {
                if (scriptAction == ScriptAction.SELECT)
                {
                    return await GenerateRoutineCallScript(dbObject, filter);
                }
            }
            else if (dbObject is Procedure proc)
            {
                if (scriptAction == ScriptAction.EXECUTE)
                {
                    return await GenerateRoutineCallScript(dbObject, filter);
                }
            }

            var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

            if (scriptAction == ScriptAction.CREATE || scriptAction == ScriptAction.ALTER)
            {
                var dbScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(dbInterpreter);

                var scripts = dbScriptGenerator.GenerateSchemaScripts(schemaInfo).Scripts;

                var databaseType = dbInterpreter.DatabaseType;

                var sbContent = new StringBuilder();

                foreach (var script in scripts)
                {
                    if (databaseType == DatabaseType.SqlServer && script is SpliterScript)
                    {
                        continue;
                    }

                    var content = script.Content;

                    if (scriptAction == ScriptAction.ALTER && typeName != nameof(Table))
                    {
                        var objType = typeName;

                        if (typeName == nameof(TableTrigger))
                        {
                            objType = "TRIGGER";
                        }

                        var createFlag = "CREATE ";
                        var createFlagIndex = GetCreateIndex(content, createFlag);

                        if (createFlagIndex >= 0)
                        {
                            switch (databaseType)
                            {
                                case DatabaseType.SqlServer:
                                    content = content.Substring(0, createFlagIndex) + "ALTER " +
                                              content.Substring(createFlagIndex + createFlag.Length);
                                    break;
                                case DatabaseType.MySql:
                                    content =
                                        $"DROP {objType} IF EXISTS {dbInterpreter.GetQuotedString(dbObject.Name)};" +
                                        Environment.NewLine + content;
                                    break;
                                case DatabaseType.Oracle:
                                    if (!Regex.IsMatch(content, @"^(CREATE[\s]+OR[\s]+REPLACE[\s]+)",
                                            RegexOptions.IgnoreCase))
                                    {
                                        content = content.Substring(0, createFlagIndex) + "CREATE OR REPLACE " +
                                                  content.Substring(createFlagIndex + createFlag.Length);
                                    }

                                    break;
                            }
                        }
                    }

                    sbContent.AppendLine(content);
                }

                result.Script = StringHelper.ToSingleEmptyLine(sbContent.ToString().Trim());
            }
            else if (dbObject is Table table)
            {
                result.Script = GenerateTableDMLScript(schemaInfo, table, scriptAction);
            }
            else if (dbObject is View view)
            {
                result.Script = GenerateViewDMLScript(schemaInfo, view, scriptAction);
            }

            return result;
        }

        private int GetCreateIndex(string script, string createFlag)
        {
            var lines = script.Split('\n');

            var count = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith(createFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return count;
                }

                count += line.Length + 1;
            }

            return -1;
        }

        public string GenerateTableDMLScript(SchemaInfo schemaInfo, Table table, ScriptAction scriptAction)
        {
            var script = "";
            var tableName = dbInterpreter.GetQuotedDbObjectNameWithSchema(table);
            var columns = schemaInfo.TableColumns;

            switch (scriptAction)
            {
                case ScriptAction.SELECT:
                    var columnNames = dbInterpreter.GetQuotedColumnNames(columns);
                    script = $"SELECT {columnNames}{Environment.NewLine}FROM {tableName};";
                    break;
                case ScriptAction.INSERT:
                    var insertColumns = columns.Where(item => !item.IsIdentity && !item.IsComputed);
                    var insertColumnNames = string.Join(",",
                        insertColumns.Select(item => dbInterpreter.GetQuotedString(item.Name)));
                    var insertValues = string.Join(",", insertColumns.Select(item => "?"));

                    script =
                        $"INSERT INTO {tableName}({insertColumnNames}){Environment.NewLine}VALUES({insertValues});";
                    break;
                case ScriptAction.UPDATE:
                    var updateColumns = columns.Where(item => !item.IsIdentity && !item.IsComputed);
                    var setNameValues = string.Join(",",
                        updateColumns.Select(item => $"{dbInterpreter.GetQuotedString(item.Name)}=?"));

                    script =
                        $"UPDATE {tableName}{Environment.NewLine}SET {setNameValues}{Environment.NewLine}WHERE <condition>;";
                    break;
                case ScriptAction.DELETE:
                    script = $"DELETE FROM {tableName}{Environment.NewLine}WHERE <condition>;";
                    break;
            }

            return script;
        }

        public string GenerateViewDMLScript(SchemaInfo schemaInfo, View view, ScriptAction scriptAction)
        {
            var script = "";
            var viewName = dbInterpreter.GetQuotedDbObjectNameWithSchema(view);
            var columns = schemaInfo.TableColumns;

            switch (scriptAction)
            {
                case ScriptAction.SELECT:
                    var columnNames = dbInterpreter.GetQuotedColumnNames(columns);
                    script = $"SELECT {columnNames}{Environment.NewLine}FROM {viewName};";
                    break;
            }

            return script;
        }

        public async Task<ScriptGenerateResult> GenerateRoutineCallScript(DatabaseObject dbObject,
            SchemaInfoFilter filter)
        {
            var result = new ScriptGenerateResult();

            var routineName = dbInterpreter.GetQuotedDbObjectNameWithSchema(dbObject);
            List<RoutineParameter> parameters = null;
            var isFunction = dbObject is Function;
            var isProcedure = dbObject is Procedure;
            var databaseType = dbInterpreter.DatabaseType;

            var sb = new StringBuilder();

            var action = "";
            var isTableFunction = false;
            var isSqlServerProcedure = false;

            if (isFunction)
            {
                action = "SELECT";

                isTableFunction = (dbObject as Function).DataType?.ToUpper() == "TABLE";
            }
            else
            {
                if (databaseType == DatabaseType.MySql || databaseType == DatabaseType.Postgres)
                {
                    action = "CALL";
                }
                else if (databaseType == DatabaseType.SqlServer)
                {
                    action = "EXEC";

                    isSqlServerProcedure = true;
                }
            }

            if (isTableFunction)
            {
                sb.Append("* FROM ");
            }

            if (isFunction)
            {
                parameters = await dbInterpreter.GetFunctionParametersAsync(filter);
            }
            else if (isProcedure)
            {
                parameters = await dbInterpreter.GetProcedureParametersAsync(filter);
            }

            result.Parameters = parameters;

            sb.Append($"{action}{(string.IsNullOrEmpty(action) ? "" : " ")}{routineName}");

            if (!isSqlServerProcedure)
            {
                sb.Append("(");
            }

            sb.AppendLine();

            if (parameters != null && parameters.Count > 0)
            {
                sb.AppendLine(string.Join(" ," + Environment.NewLine,
                    parameters.Select(item => GetRoutineParameterItem(item, isFunction))));
            }

            if (!isSqlServerProcedure)
            {
                sb.AppendLine(")");
            }

            if (isFunction && !isTableFunction && databaseType == DatabaseType.Oracle)
            {
                sb.Append("FROM DUAL");
            }

            result.Script = sb.ToString();

            return result;
        }

        public string GetRoutineParameterItem(RoutineParameter parameter, bool isFunction)
        {
            var strInOut = isFunction ? "" : parameter.IsOutput ? "OUT " : "IN ";

            return $"<{strInOut}{parameter.Name} {parameter.DataType}>";
        }
    }
}