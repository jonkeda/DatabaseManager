using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model.DatabaseObject;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Statement.Cursor;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class PlSqlStatementScriptBuilder : StatementScriptBuilder
    {
        public override StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            base.Build(statement, appendSeparator);

            var startIndex = Length;

            if (statement is SelectStatement select)
            {
                BuildSelectStatement(select, appendSeparator);
            }
            else if (statement is UnionStatement union)
            {
                AppendLine(GetUnionTypeName(union.Type));
                Build(union.SelectStatement);
            }
            else if (statement is InsertStatement insert)
            {
                AppendLine($"INSERT INTO {insert.TableName}");

                if (insert.Columns.Count > 0)
                    AppendLine($"({string.Join(",", insert.Columns.Select(item => item.ToString()))})");

                if (insert.SelectStatements != null && insert.SelectStatements.Count > 0)
                    AppendChildStatements(insert.SelectStatements);
                else
                    AppendLine($"VALUES({string.Join(",", insert.Values.Select(item => item))});");

                ReplaceTemporaryTableContent(insert.TableName, startIndex);
            }
            else if (statement is UpdateStatement update)
            {
                var fromItemsCount = update.FromItems == null ? 0 : update.FromItems.Count;

                var tableName = StatementScriptBuilderHelper.GetUpdateSetTableName(update);
                var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(update.FromItems);

                AppendLine("UPDATE");

                var tableNames = new List<TableName>();

                string strTableName = null;

                if (tableName != null)
                {
                    strTableName = tableName.Symbol;

                    tableNames.Add(tableName);
                }

                if (fromItemsCount > 0)
                {
                    var nameValues = new List<NameValueItem>();

                    var nameValueItem = new NameValueItem
                    {
                        Name = new TokenInfo($"({string.Join(",", update.SetItems.Select(item => item.Name))})")
                    };

                    var colNames = string.Join(",", update.SetItems.Select(item => item.Value));

                    var sb = new StringBuilder();

                    sb.Append($"(SELECT {colNames} ");

                    if (!hasJoin)
                    {
                        sb.Append("FROM ");

                        var lastHasNewLine = false;

                        for (var i = 0; i < fromItemsCount; i++)
                        {
                            var item = update.FromItems[i];

                            if (item.TableName != null)
                            {
                                sb.Append(item.TableName.NameWithAlias);

                                lastHasNewLine = false;
                            }
                            else if (item.SubSelectStatement != null)
                            {
                                var alias = item.Alias == null ? "" : item.Alias.Symbol;

                                var builder = new PlSqlStatementScriptBuilder();

                                builder.AppendLine("(");

                                builder.BuildSelectStatement(item.SubSelectStatement, false);
                                builder.Append($") {alias}");

                                sb.AppendLine(builder.ToString());

                                lastHasNewLine = true;
                            }

                            if (i < fromItemsCount - 1) sb.Append(",");
                        }

                        if (!lastHasNewLine) sb.AppendLine();
                    }
                    else
                    {
                        var i = 0;

                        foreach (var fromItem in update.FromItems)
                        {
                            var tn = fromItem.TableName.Symbol;
                            var talias = fromItem.TableName.Alias?.ToString();

                            var j = 0;

                            foreach (var joinItem in fromItem.JoinItems)
                            {
                                if (j == 0)
                                    sb.Append("FROM");
                                else
                                    sb.Append(joinItem.Type + " JOIN");

                                var joinTableName = j == 0 ? $" JOIN {fromItem.TableName.NameWithAlias}" : "";

                                sb.Append(
                                    $" {GetNameWithAlias(joinItem.TableName)}{joinTableName} ON {joinItem.Condition}{Environment.NewLine}");

                                j++;
                            }

                            i++;
                        }
                    }

                    if (update.Condition != null && update.Condition.Symbol != null)
                        sb.AppendLine($" WHERE {update.Condition}");

                    sb.Append(")");

                    nameValueItem.Value = new TokenInfo(sb.ToString());

                    nameValues.Add(nameValueItem);

                    update.SetItems = nameValues;
                }

                if (tableNames.Count == 0 && update.TableNames.Count > 0) tableNames.AddRange(update.TableNames);

                Append($" {string.Join(",", tableNames.Select(item => item.NameWithAlias))}", false);

                AppendLine("SET");

                var k = 0;

                foreach (var item in update.SetItems)
                {
                    Append($"{item.Name}=");

                    BuildUpdateSetValue(item);

                    if (k < update.SetItems.Count - 1) Append(",");

                    AppendLine(Indent);

                    k++;
                }

                if (update.Condition != null && update.Condition.Symbol != null)
                {
                    var children = update.Condition.Children;

                    var hasOtherTableOrAlias = false;

                    foreach (var child in children)
                    {
                        var symbol = child.Symbol;

                        if (symbol?.Contains(".") == true)
                        {
                            var items = symbol.Split('.');

                            var tnOrAlias = items[items.Length - 2].Trim();

                            if (!tableNames.Any(item =>
                                    item.Symbol.ToLower() == tnOrAlias ||
                                    (item.Alias != null && item.Alias.Symbol.ToLower() == tnOrAlias)))
                            {
                                hasOtherTableOrAlias = true;
                                break;
                            }
                        }
                    }

                    if (!hasOtherTableOrAlias) AppendLine($"WHERE {update.Condition}");
                }

                AppendLine(";");

                ReplaceTemporaryTableContent(tableNames.FirstOrDefault(), startIndex);
            }
            else if (statement is DeleteStatement delete)
            {
                var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(delete.FromItems);

                if (!hasJoin)
                {
                    AppendLine($"DELETE FROM {GetNameWithAlias(delete.TableName)}");

                    if (delete.Condition != null) AppendLine($"WHERE {delete.Condition}");
                }
                else
                {
                    var tableName = delete.TableName.Symbol;

                    AppendLine($"DELETE FROM {delete.TableName}");

                    string alias = null;

                    var i = 0;

                    foreach (var fromItem in delete.FromItems)
                    {
                        if (i == 0)
                        {
                            if (fromItem.TableName != null && fromItem.TableName.Alias != null)
                                alias = fromItem.TableName.Alias.Symbol;
                            else if (fromItem.Alias != null) alias = fromItem.Alias.Symbol;
                        }

                        i++;
                    }

                    //use placeholder, the actual column name should according to the business needs.
                    AppendLine("WHERE $columnName$ IN (SELECT $columnName$");

                    BuildFromItems(delete.FromItems, null, true);

                    if (delete.Condition != null) AppendLine($"WHERE {delete.Condition}");

                    AppendLine(")");
                }

                AppendLine(";");

                ReplaceTemporaryTableContent(delete.TableName, startIndex);
            }
            else if (statement is DeclareVariableStatement declareVar)
            {
                var sb = new StringBuilder();

                var defaultValue = declareVar.DefaultValue == null ? "" : $" :={declareVar.DefaultValue}";

                sb.Append($"DECLARE {declareVar.Name} {declareVar.DataType}{defaultValue};");

                if (!(Option != null && Option.NotBuildDeclareStatement))
                {
                    AppendLine(sb.ToString());
                }
                else
                {
                    if (Option.OutputRemindInformation)
                        PrintMessage($"'{StringHelper.HandleSingleQuotationChar(sb.ToString())}'");
                }

                if (Option != null && Option.CollectDeclareStatement) DeclareVariableStatements.Add(declareVar);
            }
            else if (statement is DeclareTableStatement declareTable)
            {
                var sql = BuildTable(declareTable.TableInfo);

                if (RoutineType == RoutineType.PROCEDURE) sql = GetExecuteImmediateSql(sql);

                AppendLine(sql);
            }
            else if (statement is CreateTableStatement createTable)
            {
                var sql = BuildTable(createTable.TableInfo);

                if (RoutineType == RoutineType.PROCEDURE && createTable.TableInfo.IsTemporary)
                {
                    TemporaryTableNames.Add(createTable.TableInfo.Name.Symbol);

                    sql = GetExecuteImmediateSql(sql);
                }

                AppendLine(sql);
            }
            else if (statement is IfStatement @if)
            {
                foreach (var item in @if.Items)
                {
                    if (item.Type == IfStatementType.IF || item.Type == IfStatementType.ELSEIF)
                    {
                        Append($"{(item.Type == IfStatementType.ELSEIF ? "ELSIF" : "IF")} ");

                        BuildIfCondition(item);

                        AppendLine(" THEN");
                    }
                    else
                    {
                        AppendLine($"{item.Type}");
                    }

                    AppendLine("BEGIN");

                    AppendChildStatements(item.Statements);

                    AppendLine("END;");
                }

                AppendLine("END IF;");
            }
            else if (statement is CaseStatement @case)
            {
                AppendLine($"CASE {@case.VariableName}");

                foreach (var item in @case.Items)
                {
                    if (item.Type != IfStatementType.ELSE)
                        AppendLine($"WHEN {item.Condition} THEN");
                    else
                        AppendLine("ELSE");

                    AppendLine("BEGIN");
                    AppendChildStatements(item.Statements);
                    AppendLine("END;");
                }

                AppendLine("END CASE;");
            }
            else if (statement is SetStatement set)
            {
                if (set.Key != null)
                {
                    if (set.Value != null)
                    {
                        if (set.IsSetUserVariable)
                        {
                            var dataType =
                                AnalyserHelper.GetUserVariableDataType(DatabaseType.Oracle, set.UserVariableDataType);

                            if (!string.IsNullOrEmpty(dataType))
                            {
                                var declareVariable = new DeclareVariableStatement
                                    { Name = set.Key, DataType = new TokenInfo(dataType) };

                                DeclareVariableStatements.Add(declareVariable);
                            }
                        }

                        var value = set.Value.Symbol;

                        value = GetSetVariableValue(set.Key.Symbol, set.Value?.Symbol);

                        if (!AnalyserHelper.IsSubQuery(value))
                            AppendLine($"{set.Key} := {value};");
                        else
                            AppendLine(StatementScriptBuilderHelper.ConvertToSelectIntoVariable(set.Key.Symbol, value));
                    }
                    else if (set.IsSetCursorVariable && set.ValueStatement != null)
                    {
                        var declareCursorStatement =
                            DeclareCursorStatements.FirstOrDefault(item => item.CursorName.Symbol == set.Key.Symbol);

                        if (declareCursorStatement == null)
                        {
                            AppendLine($"SET {set.Key} =");

                            BuildSelectStatement(set.ValueStatement);
                        }
                        else
                        {
                            declareCursorStatement.SelectStatement = set.ValueStatement;
                        }
                    }
                }
            }
            else if (statement is LoopStatement loop)
            {
                if (loop.Type == LoopType.LOOP)
                {
                    AppendLine("LOOP");
                }
                else if (loop.Type == LoopType.FOR)
                {
                    var loopCursor = loop.LoopCursorInfo;

                    Append($"FOR {loopCursor.IteratorName} IN ");

                    if (loopCursor.IsIntegerIterate)
                    {
                        Append(
                            $"{(loopCursor.IsReverse ? "REVERSE" : "")} {loopCursor.StartValue}..{loopCursor.StopValue}");
                    }
                    else
                    {
                        var builder = new PlSqlStatementScriptBuilder();

                        builder.Build(loopCursor.SelectStatement, false);

                        Append($"({builder})");
                    }

                    AppendLine(" LOOP");
                }
                else
                {
                    AppendLine($"{loop.Type.ToString()} {loop.Condition} LOOP");
                }

                AppendChildStatements(loop.Statements);
                AppendLine("END LOOP;");
            }
            else if (statement is LoopExitStatement loopExit)
            {
                AppendLine($"EXIT WHEN {loopExit.Condition};");
            }
            else if (statement is BreakStatement @break)
            {
                AppendLine("EXIT;");
            }
            else if (statement is WhileStatement @while)
            {
                var loopStatement = @while as LoopStatement;

                AppendLine($"WHILE {@while.Condition} LOOP");
                AppendChildStatements(@while.Statements);
                AppendLine("END LOOP;");
            }
            else if (statement is ReturnStatement @return)
            {
                AppendLine($"RETURN {@return.Value};");
            }
            else if (statement is PrintStatement print)
            {
                PrintMessage(print.Content.Symbol?.Replace("+", "||"));
            }
            else if (statement is CallStatement call)
            {
                if (!call.IsExecuteSql)
                {
                    AppendLine(
                        $"{call.Name}({string.Join(",", call.Parameters.Select(item => item.Value?.Symbol?.Split('=')?.LastOrDefault()))});");
                }
                else
                {
                    var content = call.Parameters.FirstOrDefault()?.Value?.Symbol;

                    if (!string.IsNullOrEmpty(content))
                    {
                        var parameters = call.Parameters.Skip(1);

                        var usings = new List<CallParameter>();

                        foreach (var parameter in parameters)
                        {
                            var value = parameter.Value?.Symbol;

                            if (!parameter.IsDescription) usings.Add(parameter);
                        }

                        var strUsings = usings.Count == 0
                            ? ""
                            : $" USING {string.Join(",", usings.Select(item => $"{item.Value}"))}";

                        AppendLine($"EXECUTE IMMEDIATE {content}{strUsings};");
                    }
                }
            }
            else if (statement is TransactionStatement transaction)
            {
                var commandType = transaction.CommandType;

                switch (commandType)
                {
                    case TransactionCommandType.COMMIT:
                        AppendLine("COMMIT;");
                        break;
                    case TransactionCommandType.ROLLBACK:
                        AppendLine("ROLLBACK;");
                        break;
                }
            }
            else if (statement is LeaveStatement leave)
            {
                AppendLine("RETURN;");
            }
            else if (statement is TryCatchStatement tryCatch)
            {
                AppendLine("EXCEPTION");
                AppendLine("WHEN OTHERS THEN");
                AppendLine("BEGIN");

                AppendChildStatements(tryCatch.CatchStatements);

                AppendLine("END;");

                AppendChildStatements(tryCatch.TryStatements);
            }
            else if (statement is ExceptionStatement exception)
            {
                AppendLine("EXCEPTION");

                foreach (var exceptionItem in exception.Items)
                {
                    AppendLine($"WHEN {exceptionItem.Name} THEN");
                    AppendLine("BEGIN");

                    AppendChildStatements(exceptionItem.Statements);

                    AppendLine("END;");
                }
            }
            else if (statement is DeclareCursorStatement declareCursor)
            {
                var sb = new StringBuilder();

                sb.Append(
                    $"DECLARE CURSOR {declareCursor.CursorName}{(declareCursor.SelectStatement != null ? " IS" : "")}");

                if (!(Option != null && Option.NotBuildDeclareStatement))
                {
                    if (declareCursor.SelectStatement != null)
                    {
                        AppendLine(sb.ToString());
                        Build(declareCursor.SelectStatement);
                    }
                }
                else
                {
                    if (Option.OutputRemindInformation)
                        PrintMessage($"'{StringHelper.HandleSingleQuotationChar(sb.ToString())}'");
                }

                if (Option != null && Option.CollectDeclareStatement)
                    if (!DeclareCursorStatements.Any(item => item.CursorName.Symbol == declareCursor.CursorName.Symbol))
                        DeclareCursorStatements.Add(declareCursor);
            }
            else if (statement is OpenCursorStatement openCursor)
            {
                AppendLine($"OPEN {openCursor.CursorName};");
            }
            else if (statement is FetchCursorStatement fetchCursor)
            {
                if (fetchCursor.Variables.Count > 0)
                    AppendLine($"FETCH {fetchCursor.CursorName} INTO {string.Join(",", fetchCursor.Variables)};");
            }
            else if (statement is CloseCursorStatement closeCursor)
            {
                AppendLine($"CLOSE {closeCursor.CursorName};");
            }
            else if (statement is TruncateStatement truncate)
            {
                AppendLine($"TRUNCATE TABLE {truncate.TableName};");

                ReplaceTemporaryTableContent(truncate.TableName, startIndex);
            }
            else if (statement is DropStatement drop)
            {
                var objectType = drop.ObjectType.ToString().ToUpper();

                AppendLine($"DROP {objectType} {drop.ObjectName.NameWithSchema};");

                if (drop.ObjectType == DatabaseObjectType.Table)
                    ReplaceTemporaryTableContent(drop.ObjectName, startIndex);
            }
            else if (statement is RaiseErrorStatement error)
            {
                var code = error.ErrorCode == null ? "-20000" : error.ErrorCode.Symbol;

                AppendLine($"RAISE_APPLICATION_ERROR({code},{error.Content});");
            }
            else if (statement is GotoStatement gts)
            {
                if (gts.IsLabel)
                {
                    AppendLine($"GOTO {gts.Label};");
                }
                else
                {
                    AppendLine($"<<{gts.Label}>>");

                    AppendChildStatements(gts.Statements);
                }
            }
            else if (statement is PreparedStatement prepared)
            {
                var type = prepared.Type;

                if (type == PreparedStatementType.Prepare)
                {
                    if (Option.CollectSpecialStatementTypes.Contains(prepared.GetType()))
                        SpecialStatements.Add(prepared);
                }
                else if (type == PreparedStatementType.Execute)
                {
                    var pre = SpecialStatements.FirstOrDefault(item =>
                            item is PreparedStatement preparedStatement &&
                            preparedStatement.Id.Symbol == prepared.Id.Symbol) as
                        PreparedStatement;

                    var variables = prepared.ExecuteVariables.Count > 0
                        ? $" USING {string.Join(",", prepared.ExecuteVariables)}"
                        : "";

                    AppendLine($"EXECUTE IMMEDIATE {pre?.FromSqlOrVariable}{variables};");
                }
            }

            return this;
        }

        protected override void BuildSelectStatement(SelectStatement select, bool appendSeparator = true)
        {
            var isCreateTemporaryTable = false;

            var startIndex = Length;

            var intoTableName = AnalyserHelper.GetIntoTableName(select);

            if (intoTableName != null)
            {
                isCreateTemporaryTable = true;

                AppendLine($"CREATE GLOBAL TEMPORARY TABLE {intoTableName} AS (");
            }

            var isWith = select.WithStatements != null && select.WithStatements.Count > 0;
            var hasAssignVariableColumn = HasAssignVariableColumn(select);
            var selectColumns = $"SELECT {string.Join(",", select.Columns.Select(item => GetNameWithAlias(item)))}";

            var handled = false;

            if (select.NoTableName && hasAssignVariableColumn)
            {
                foreach (var column in select.Columns)
                {
                    var symbol = column.Symbol;

                    if (AnalyserHelper.IsAssignNameColumn(column))
                    {
                        var items = symbol.Split('=');

                        var variable = items[0];
                        var value = string.Join("=", items.Skip(1));

                        value = GetSetVariableValue(variable, value);

                        if (!AnalyserHelper.IsSubQuery(value))
                            symbol = $"{variable}:={value}";
                        else
                            symbol = StatementScriptBuilderHelper.ConvertToSelectIntoVariable(variable, value);
                    }

                    AppendLine($"{symbol};");
                }

                handled = true;
            }
            else if (!select.NoTableName && hasAssignVariableColumn && (RoutineType == RoutineType.PROCEDURE ||
                                                                        RoutineType == RoutineType.FUNCTION ||
                                                                        RoutineType == RoutineType.TRIGGER))
            {
                //use "select column1, column2 into var1, var2" instead of "select var1=column1, var2=column2"

                var variables = new List<string>();
                var columnNames = new List<string>();

                foreach (var column in select.Columns)
                    if (column.Symbol.Contains("="))
                    {
                        var items = column.Symbol.Split('=');
                        var variable = items[0].Trim();
                        var columName = items[1].Trim();

                        variables.Add(variable);
                        columnNames.Add(columName);
                    }

                AppendLine($"SELECT {string.Join(",", columnNames)} INTO {string.Join(",", variables)}");
            }
            else if (!isWith)
            {
                AppendLine(selectColumns);
            }

            if (!isCreateTemporaryTable && select.Intos != null && select.Intos.Count > 0)
            {
                Append("INTO ");
                AppendLine(string.Join(",", select.Intos));
            }

            if (!handled)
                if (select.TableName == null && !select.HasFromItems)
                    select.TableName = new TableName("DUAL");

            Action appendWith = () =>
            {
                var i = 0;

                foreach (var withStatement in select.WithStatements)
                {
                    if (i == 0)
                        AppendLine($"WITH {withStatement.Name}");
                    else
                        AppendLine($",{withStatement.Name}");

                    AppendLine("AS(");

                    AppendChildStatements(withStatement.SelectStatements, false);

                    AppendLine(")");

                    i++;
                }
            };

            Action appendFrom = () =>
            {
                if (select.HasFromItems)
                    BuildSelectStatementFromItems(select);
                else if (select.TableName != null) AppendLine($"FROM {GetNameWithAlias(select.TableName)}");
            };

            if (isWith)
            {
                appendWith();

                AppendLine(selectColumns);
            }

            appendFrom();

            if (select.Where != null) AppendLine($"WHERE {select.Where}");

            if (select.GroupBy != null && select.GroupBy.Count > 0)
                AppendLine($"GROUP BY {string.Join(",", select.GroupBy)}");

            if (select.Having != null) AppendLine($"HAVING {select.Having}");

            if (select.OrderBy != null && select.OrderBy.Count > 0)
                AppendLine($"ORDER BY {string.Join(",", select.OrderBy)}");

            if (select.TopInfo != null) AppendLine($"FETCH NEXT {select.TopInfo.TopCount} ROWS ONLY");

            if (select.LimitInfo != null)
                AppendLine(
                    $"OFFSET {select.LimitInfo.StartRowIndex?.Symbol ?? "0"} ROWS FETCH NEXT {select.LimitInfo.RowCount} ROWS ONLY");

            if (select.UnionStatements != null)
                foreach (var union in select.UnionStatements)
                {
                    Build(union, false).TrimSeparator();
                    AppendLine();
                }

            if (isCreateTemporaryTable) AppendLine(")");

            if (appendSeparator) AppendLine(";");

            if (isCreateTemporaryTable)
            {
                TemporaryTableNames.Add(intoTableName.Symbol);

                ReplaceTemporaryTableContent(intoTableName, startIndex);
            }
            else
            {
                var tn = StatementScriptBuilderHelper.GetSelectStatementTableName(select);

                ReplaceTemporaryTableContent(tn, startIndex);
            }
        }

        private string GetUnionTypeName(UnionType unionType)
        {
            switch (unionType)
            {
                case UnionType.UNION_ALL:
                    return "UNION ALL";
                case UnionType.EXCEPT:
                    return nameof(UnionType.MINUS);
                default:
                    return unionType.ToString();
            }
        }

        private void PrintMessage(string content)
        {
            AppendLine($"DBMS_OUTPUT.PUT_LINE({content});");
        }

        public string BuildTable(TableInfo table)
        {
            var sb = new StringBuilder();

            var tableName = table.Name.Symbol;

            if (!table.IsGlobal) tableName = GetPrivateTemporaryTableName(tableName);

            var columns = table.Columns;
            var selectStatement = table.SelectStatement;

            var temporaryType = table.IsTemporary ? table.IsGlobal ? "GLOBAL" : "PRIVATE" : "";

            sb.AppendLine(
                $"CREATE {temporaryType} {(table.IsTemporary ? "TEMPORARY" : "")} TABLE {tableName}{(columns.Count > 0 ? "(" : "AS")}");

            if (columns.Count > 0)
            {
                var hasTableConstraints = table.HasTableConstraints;

                var i = 0;

                foreach (var column in columns)
                {
                    var name = column.Name.Symbol;
                    var dataType = column.DataType?.Symbol ?? "";
                    var require = column.IsNullable ? " NULL" : " NOT NULL";
                    var seperator = i == table.Columns.Count - 1 ? hasTableConstraints ? "," : "" : ",";

                    if (column.IsComputed)
                    {
                        sb.Append($"{name} AS ({column.ComputeExp}){require}{seperator}");
                    }
                    else
                    {
                        var identity = column.IsIdentity ? " GENERATED ALWAYS AS IDENTITY" : "";
                        var defaultValue = string.IsNullOrEmpty(column.DefaultValue?.Symbol)
                            ? ""
                            : $" DEFAULT {StringHelper.GetParenthesisedString(column.DefaultValue.Symbol)}";
                        var constraint = GetConstraints(column.Constraints, true);
                        var strConstraint = string.IsNullOrEmpty(constraint) ? "" : $" {constraint}";

                        sb.AppendLine(
                            $"{name} {column.DataType}{defaultValue}{identity}{require}{strConstraint}{seperator}");
                    }

                    i++;
                }

                if (hasTableConstraints) sb.AppendLine(GetConstraints(table.Constraints));

                sb.AppendLine(")");

                if (!table.IsGlobal) sb.Append("ON COMMIT DROP DEFINITION");
            }
            else
            {
                var builder = new PlSqlStatementScriptBuilder();

                builder.BuildSelectStatement(selectStatement, false);

                sb.AppendLine(builder.ToString());
            }

            sb.Append(";");

            return sb.ToString();
        }

        private string GetPrivateTemporaryTableName(string tableName)
        {
            var prefix = "ORA$PTT_";

            if (!tableName.ToUpper().StartsWith(prefix))
            {
                var newTableName = prefix + tableName;

                if (!Replacements.ContainsKey(tableName)) Replacements.Add(tableName, newTableName);

                if (!TemporaryTableNames.Contains(tableName)) TemporaryTableNames.Add(tableName);

                return newTableName;
            }

            return tableName;
        }

        private string GetSetVariableValue(string name, string value)
        {
            if (name != null && value != null && ValueHelper.IsStringValue(value))
            {
                var isTimestampValue = value.Contains(" ");
                var dateFormat = "'yyyy-MM-dd'";
                var datetimeFormat = "'yyyy-MM-dd HH24:mi:ss'";

                var format = isTimestampValue ? datetimeFormat : dateFormat;

                var declareVariable =
                    DeclareVariableStatements.FirstOrDefault(item => item.Name.Symbol?.Trim() == name.Trim());

                var dataType = declareVariable?.DataType?.Symbol?.ToUpper();

                if (dataType != null)
                    if (dataType == "DATE" || dataType.Contains("TIMESTAMP"))
                        value = $"TO_DATE({value}, {format})";
            }

            return value;
        }

        private string GetExecuteImmediateSql(string sql)
        {
            return $"EXECUTE IMMEDIATE  '{StringHelper.HandleSingleQuotationChar(sql)}';";
        }

        private bool IsTemporaryTable(TokenInfo tableName)
        {
            if (tableName == null || tableName.Symbol == null) return false;

            var strTableName = tableName.Symbol;

            return TemporaryTableNames.Contains(strTableName);
        }

        private void ReplaceTemporaryTableContent(TokenInfo tableName, int startIndex)
        {
            if (RoutineType == RoutineType.PROCEDURE && IsTemporaryTable(tableName))
            {
                var length = Length - startIndex;

                if (length > 0)
                {
                    var content = Script.ToString().Substring(startIndex, length);

                    Script.Replace(content, GetExecuteImmediateSql(content), startIndex, length);
                }
            }
        }

        protected override bool IsInvalidTableName(string tableName)
        {
            if (tableName == null) return false;

            tableName = GetTrimedQuotationValue(tableName);

            if (tableName.ToUpper() == "DUAL" && !(this is PlSqlStatementScriptBuilder)) return true;

            return false;
        }
    }
}