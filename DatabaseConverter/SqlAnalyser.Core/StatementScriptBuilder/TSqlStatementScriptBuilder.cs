﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class TSqlStatementScriptBuilder : StatementScriptBuilder
    {
        public override StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            base.Build(statement, appendSeparator);

            if (statement is SelectStatement select)
            {
                BuildSelectStatement(select, appendSeparator);
            }
            else if (statement is InsertStatement insert)
            {
                Append($"INSERT INTO {insert.TableName}", false);

                if (insert.Columns.Count > 0) AppendLine($"({string.Join(",", insert.Columns.Select(item => item))})");

                if (insert.SelectStatements != null && insert.SelectStatements.Count > 0)
                    AppendChildStatements(insert.SelectStatements);
                else
                    AppendLine($"VALUES({string.Join(",", insert.Values.Select(item => item))});");
            }
            else if (statement is UnionStatement union)
            {
                AppendLine(GetUnionTypeName(union.Type));
                Build(union.SelectStatement);
            }
            else if (statement is UpdateStatement update)
            {
                var fromItemsCount = update.FromItems == null ? 0 : update.FromItems.Count;
                var tableName = StatementScriptBuilderHelper.GetUpdateSetTableName(update);

                if (tableName == null && update.TableNames != null) tableName = update.TableNames.FirstOrDefault();

                var useAlias = tableName.Alias != null;

                AppendLine($"UPDATE {(useAlias ? tableName.Alias.Symbol : tableName.Symbol)}");

                Append("SET ");

                if (!StatementScriptBuilderHelper.IsCompositeUpdateSetColumnName(update))
                {
                    var k = 0;

                    foreach (var item in update.SetItems)
                    {
                        Append($"{item.Name}=");

                        BuildUpdateSetValue(item);

                        if (k < update.SetItems.Count - 1) Append(",");

                        AppendLine(Indent);

                        k++;
                    }
                }
                else if (update.SetItems.Count > 0)
                {
                    Append(StatementScriptBuilderHelper.ParseCompositeUpdateSet(this, update));

                    return this;
                }

                var sb = new StringBuilder();

                if (fromItemsCount > 0)
                {
                    var i = 0;

                    foreach (var fromItem in update.FromItems)
                    {
                        var hasJoin = fromItem.HasJoinItems;

                        if (hasJoin)
                        {
                            if (i == 0) AppendLine($"FROM {fromItem.TableName.NameWithAlias}");

                            foreach (var joinItem in fromItem.JoinItems)
                            {
                                var condition = joinItem.Condition == null ? "" : $" ON {joinItem.Condition}";

                                AppendLine($"{joinItem.Type} JOIN {joinItem.TableName.NameWithAlias}{condition}");
                            }
                        }
                        else
                        {
                            if (i == 0)
                                Append("FROM ");
                            else
                                Append(",");

                            if (fromItem.SubSelectStatement != null)
                            {
                                var alias = fromItem.Alias == null ? "" : fromItem.Alias.Symbol;

                                AppendLine("(");
                                BuildSelectStatement(fromItem.SubSelectStatement, false);
                                AppendLine($") {alias}");
                            }
                            else if (fromItem.TableName != null)
                            {
                                Append($"{fromItem.TableName.NameWithAlias}");
                            }
                        }

                        i++;
                    }
                }
                else
                {
                    if (useAlias) AppendLine($"FROM {tableName.NameWithAlias}");
                }

                if (update.Condition != null && update.Condition.Symbol != null)
                    AppendLine($"WHERE {update.Condition}");

                if (update.Option != null) AppendLine(update.Option.ToString());

                AppendLine(";");
            }
            else if (statement is DeleteStatement delete)
            {
                var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(delete.FromItems);

                if (!hasJoin)
                {
                    AppendLine($"DELETE FROM {delete.TableName}");
                }
                else
                {
                    AppendLine($"DELETE {delete.TableName}");

                    BuildFromItems(delete.FromItems);
                }

                if (delete.Condition != null) AppendLine($"WHERE {delete.Condition}");

                AppendLine(";");
            }
            else if (statement is DeclareVariableStatement declareVar)
            {
                var defaultValue = declareVar.DefaultValue == null ? "" : $" = {declareVar.DefaultValue}";
                AppendLine($"DECLARE {declareVar.Name} {declareVar.DataType}{defaultValue};");
            }
            else if (statement is DeclareTableStatement declareTable)
            {
                var tableInfo = declareTable.TableInfo;

                AppendLine($"DECLARE {tableInfo.Name} TABLE (");

                var i = 0;

                foreach (var column in tableInfo.Columns)
                    AppendLine(
                        $"{column.Name.FieldName} {column.DataType}{(i == tableInfo.Columns.Count - 1 ? "" : ",")}");

                AppendLine(")");
            }
            else if (statement is CreateTableStatement createTable)
            {
                AppendLine(BuildTable(createTable.TableInfo));
            }
            else if (statement is SetStatement set)
            {
                if (set.Key != null && set.Value != null)
                {
                    if (set.IsSetUserVariable)
                    {
                        var dataType =
                            AnalyserHelper.GetUserVariableDataType(DatabaseType.SqlServer, set.UserVariableDataType);

                        if (!string.IsNullOrEmpty(dataType)) AppendLine($"DECLARE {set.Key} {dataType};");
                    }

                    var valueToken = set.Value;

                    if (valueToken != null)
                    {
                        if (IsRoutineName(valueToken))
                        {
                            MakeupRoutineName(valueToken);
                        }
                        else
                        {
                            var child = valueToken.Children.FirstOrDefault(item => IsRoutineName(item));

                            if (child != null) MakeupRoutineName(valueToken);
                        }
                    }

                    AppendLine($"SET {set.Key} = {set.Value};");
                }
            }

            if (statement is IfStatement @if)
            {
                foreach (var item in @if.Items)
                {
                    if (item.Type == IfStatementType.IF || item.Type == IfStatementType.ELSEIF)
                    {
                        Append($"{item.Type} ");

                        BuildIfCondition(item);

                        AppendLine();
                    }
                    else
                    {
                        AppendLine($"{item.Type}");
                    }

                    AppendLine("BEGIN");

                    if (item.Statements.Count > 0)
                        AppendChildStatements(item.Statements);
                    else
                        AppendLine("PRINT('BLANK!');");

                    AppendLine("END");
                    AppendLine();
                }
            }
            else if (statement is CaseStatement @case)
            {
                var variableName = @case.VariableName.ToString();

                var ifStatement = new IfStatement();

                var i = 0;
                foreach (var item in @case.Items)
                {
                    var ifItem = new IfStatementItem();

                    ifItem.Type = i == 0 ? IfStatementType.IF : item.Type;

                    if (item.Type != IfStatementType.ELSE)
                        ifItem.Condition = new TokenInfo($"{variableName}={item.Condition}")
                            { Type = TokenType.IfCondition };

                    i++;
                }

                Build(ifStatement);
            }
            else if (statement is LoopStatement loop)
            {
                var isReverse = false;
                var isForLoop = false;
                var isIntegerIterate = false;
                string iteratorName = null;

                if (loop.Type != LoopType.FOR)
                {
                    AppendLine($"WHILE {loop.Condition}");
                }
                else if (loop.LoopCursorInfo != null)
                {
                    isForLoop = true;
                    isReverse = loop.LoopCursorInfo.IsReverse;

                    iteratorName = "@" + loop.LoopCursorInfo.IteratorName.Symbol;

                    if (loop.LoopCursorInfo.IsIntegerIterate)
                    {
                        isIntegerIterate = true;
                        AppendLine($"DECLARE {iteratorName} INT;");

                        if (!isReverse)
                        {
                            AppendLine($"SET {iteratorName}={loop.LoopCursorInfo.StartValue};");
                            AppendLine($"WHILE {iteratorName}<={loop.LoopCursorInfo.StopValue}");
                        }
                        else
                        {
                            AppendLine($"SET {iteratorName}={loop.LoopCursorInfo.StopValue};");
                            AppendLine($"WHILE {iteratorName}>={loop.LoopCursorInfo.StartValue}");
                        }
                    }
                }

                AppendLine("BEGIN");

                AppendChildStatements(loop.Statements);

                if (isForLoop && isIntegerIterate)
                    AppendLine($"SET {iteratorName}= {iteratorName}{(isReverse ? "-" : "+")}1;");

                AppendLine("END");
            }
            else if (statement is WhileStatement @while)
            {
                AppendLine($"WHILE {@while.Condition}");
                AppendLine("BEGIN");

                AppendChildStatements(@while.Statements);

                AppendLine("END");
                AppendLine();
            }
            else if (statement is LoopExitStatement whileExit)
            {
                if (!whileExit.IsCursorLoopExit)
                {
                    AppendLine($"IF {whileExit.Condition}");
                    AppendLine("BEGIN");
                    AppendLine("BREAK");
                    AppendLine("END");
                }
            }
            else if (statement is TryCatchStatement tryCatch)
            {
                AppendLine("BEGIN TRY");
                AppendChildStatements(tryCatch.TryStatements);
                AppendLine("END TRY");

                AppendLine("BEGIN CATCH");
                AppendChildStatements(tryCatch.CatchStatements);
                AppendLine("END CATCH");
            }
            else if (statement is ReturnStatement @return)
            {
                AppendLine($"RETURN {@return.Value};");
            }
            else if (statement is BreakStatement @break)
            {
                AppendLine("BREAK;");
            }
            else if (statement is ContinueStatement @continue)
            {
                AppendLine("CONTINUE;");
            }
            else if (statement is PrintStatement print)
            {
                AppendLine($"PRINT {print.Content.Symbol?.Replace("||", "+")};");
            }
            else if (statement is CallStatement call)
            {
                if (!call.IsExecuteSql)
                {
                    AppendLine($"EXECUTE {call.Name} {string.Join(",", call.Parameters.Select(item => item.Value))};");
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

                        var strParameters = usings.Count == 0
                            ? ""
                            : $",N'', {string.Join(",", usings.Select(item => $"{item.Value}"))}";

                        AppendLine($"EXECUTE SP_EXECUTESQL {content}{strParameters};");
                    }
                }
            }
            else if (statement is TransactionStatement transaction)
            {
                var commandType = transaction.CommandType;

                var content = transaction.Content == null ? "" : $" {transaction.Content.Symbol}";

                switch (commandType)
                {
                    case TransactionCommandType.BEGIN:
                        AppendLine($"BEGIN TRANS{content};");
                        break;
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
            else if (statement is DeclareCursorStatement declareCursor)
            {
                AppendLine(
                    $"DECLARE {declareCursor.CursorName} CURSOR{(declareCursor.SelectStatement != null ? " FOR" : "")}");

                if (declareCursor.SelectStatement != null) Build(declareCursor.SelectStatement);

                AppendLine();
            }
            else if (statement is OpenCursorStatement openCursor)
            {
                AppendLine($"OPEN {openCursor.CursorName}");
            }
            else if (statement is FetchCursorStatement fetchCursor)
            {
                AppendLine($"FETCH NEXT FROM {fetchCursor.CursorName} INTO {string.Join(",", fetchCursor.Variables)}");
            }
            else if (statement is CloseCursorStatement closeCursor)
            {
                AppendLine($"CLOSE {closeCursor.CursorName}");

                if (closeCursor.IsEnd) AppendLine($"DEALLOCATE {closeCursor.CursorName}");
            }
            else if (statement is DeallocateCursorStatement deallocateCursor)
            {
                AppendLine($"DEALLOCATE {deallocateCursor.CursorName}");
            }
            else if (statement is TruncateStatement truncate)
            {
                AppendLine($"TRUNCATE TABLE {truncate.TableName};");
            }
            else if (statement is DropStatement drop)
            {
                var objectType = drop.ObjectType.ToString().ToUpper();

                AppendLine($"DROP {objectType} IF EXISTS {drop.ObjectName.NameWithSchema};");
            }
            else if (statement is RaiseErrorStatement error)
            {
                var severity = string.IsNullOrEmpty(error.Severity) ? "-1" : error.Severity;
                var state = string.IsNullOrEmpty(error.State) ? "0" : error.State;

                AppendLine($"RAISERROR({error.Content},{severity},{state});");
            }
            else if (statement is GotoStatement gts)
            {
                if (gts.IsLabel)
                {
                    AppendLine($"GOTO {gts.Label};");
                }
                else
                {
                    AppendLine($"{gts.Label}:");

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
                            item is PreparedStatement preparedStatement && preparedStatement.Id.Symbol == prepared.Id.Symbol) as
                        PreparedStatement;

                    var variables = prepared.ExecuteVariables.Count > 0
                        ? $",N'',{string.Join(",", prepared.ExecuteVariables)}"
                        : "";

                    AppendLine($"EXECUTE SP_EXECUTESQL {pre?.FromSqlOrVariable}{variables};");
                }
            }

            return this;
        }

        private bool IsRoutineName(TokenInfo token)
        {
            var tokenType = token.Type;

            return tokenType == TokenType.RoutineName || tokenType == TokenType.ProcedureName ||
                   tokenType == TokenType.FunctionName;
        }

        protected override void BuildSelectStatement(SelectStatement select, bool appendSeparator = true)
        {
            var intoTableName = AnalyserHelper.GetIntoTableName(select);
            var isAssignVariable = intoTableName == null && select.Intos != null && select.Intos.Count > 0;

            var isWith = select.WithStatements != null && select.WithStatements.Count > 0;

            if (select.LimitInfo != null && select.TopInfo == null)
                if (select.LimitInfo.StartRowIndex == null || select.LimitInfo.StartRowIndex.Symbol == "0")
                    select.TopInfo = new SelectTopInfo { TopCount = select.LimitInfo.RowCount };

            var top = select.TopInfo == null
                ? ""
                : $" TOP {select.TopInfo.TopCount}{(select.TopInfo.IsPercent ? " PERCENT " : " ")}";

            var selectColumns = $"SELECT {top}";

            if (!isAssignVariable)
                selectColumns += $"{string.Join(",", select.Columns.Select(item => GetNameWithAlias(item)))}";

            if (!isWith) Append(selectColumns);

            if (intoTableName != null)
            {
                AppendLine($"INTO {intoTableName}");
            }
            else if (isAssignVariable && select.Columns.Count == select.Intos.Count)
            {
                var assigns = new List<string>();

                for (var i = 0; i < select.Intos.Count; i++) assigns.Add($"{select.Intos[i]}={select.Columns[i]}");

                AppendLine(string.Join(", ", assigns));
            }

            Action appendWith = () =>
            {
                var i = 0;

                foreach (var withStatement in select.WithStatements)
                {
                    if (i == 0)
                    {
                        AppendLine($"WITH {withStatement.Name}");

                        if (withStatement.Columns != null && withStatement.Columns.Count > 0)
                            AppendLine($"({string.Join(",", withStatement.Columns.Select(item => item))})");
                    }
                    else
                    {
                        AppendLine($",{withStatement.Name}");
                    }

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

            if (select.Where != null) Append($"WHERE {select.Where}");

            if (select.GroupBy != null && select.GroupBy.Count > 0)
                AppendLine($"GROUP BY {string.Join(",", select.GroupBy.Select(item => item))}");

            if (select.Having != null) AppendLine($"HAVING {select.Having}");

            if (select.OrderBy != null && select.OrderBy.Count > 0)
                AppendLine($"ORDER BY {string.Join(",", select.OrderBy.Select(item => item))}");

            if (select.LimitInfo != null)
                if (select.TopInfo == null)
                {
                    if (select.OrderBy == null) AppendLine("ORDER BY (SELECT 0)");

                    //NOTE: "OFFSET X ROWS FETCH NEXT Y ROWS ONLY" only available for SQLServer 2012 and above.
                    AppendLine(
                        $"OFFSET {select.LimitInfo.StartRowIndex?.Symbol ?? "0"} ROWS FETCH NEXT {select.LimitInfo.RowCount} ROWS ONLY");
                }

            if (select.UnionStatements != null)
                foreach (var union in select.UnionStatements)
                {
                    Build(union, false).TrimSeparator();
                    AppendLine();
                }

            if (appendSeparator) AppendLine(";");
        }

        private void MakeupRoutineName(TokenInfo token)
        {
            var symbol = token.Symbol;
            var index = symbol.IndexOf("(", StringComparison.Ordinal);

            var name = index == -1 ? symbol : symbol.Substring(0, index);

            if (!name.Contains(".")) token.Symbol = "dbo." + symbol;
        }

        private string GetUnionTypeName(UnionType unionType)
        {
            switch (unionType)
            {
                case UnionType.UNION_ALL:
                    return "UNION ALL";
                case UnionType.MINUS:
                    return nameof(UnionType.EXCEPT);
                default:
                    return unionType.ToString();
            }
        }

        protected override string GetPivotInItem(TokenInfo token)
        {
            return $"[{GetTrimedQuotationValue(token.Symbol)}]";
        }

        public string BuildTable(TableInfo table)
        {
            var sb = new StringBuilder();

            var tableName = table.Name.Symbol;

            var trimedTableName = tableName.Trim('[', ']');

            if (table.IsTemporary && !trimedTableName.StartsWith("#"))
            {
                var newTableName = "#" + trimedTableName;

                if (!Replacements.ContainsKey(tableName)) Replacements.Add(trimedTableName, newTableName);
            }

            var hasColumns = table.Columns.Count > 0;
            var hasSelect = table.SelectStatement != null;

            if (hasColumns)
            {
                sb.AppendLine($"CREATE TABLE {tableName}(");

                var hasTableConstraints = table.HasTableConstraints;

                var i = 0;

                foreach (var column in table.Columns)
                {
                    var name = column.Name.Symbol;
                    var dataType = column.DataType?.Symbol ?? "VARCHAR(MAX)";
                    var require = column.IsNullable ? " NULL" : " NOT NULL";
                    var seperator = i == table.Columns.Count - 1 ? hasTableConstraints ? "," : "" : ",";

                    var isComputeExp = column.IsComputed;

                    if (isComputeExp)
                    {
                        sb.Append($"{name} AS ({column.ComputeExp}){seperator}");
                    }
                    else
                    {
                        var identity = column.IsIdentity
                            ? $" IDENTITY({table.IdentitySeed ?? 1},{table.IdentityIncrement ?? 1})"
                            : "";
                        var defaultValue = string.IsNullOrEmpty(column.DefaultValue?.Symbol)
                            ? ""
                            : $" DEFAULT {StringHelper.GetParenthesisedString(column.DefaultValue.Symbol)}";
                        var constraint = GetConstriants(column.Constraints, true);
                        var strConstraint = string.IsNullOrEmpty(constraint) ? "" : $" {constraint}";

                        sb.AppendLine(
                            $"{name} {column.DataType}{identity}{require}{defaultValue}{strConstraint}{seperator}");
                    }

                    i++;
                }

                if (hasTableConstraints) sb.AppendLine(GetConstriants(table.Constraints));

                sb.Append(")");
            }
            else if (hasSelect)
            {
                table.SelectStatement.Intos = new List<TokenInfo>();

                table.SelectStatement.Intos.Add(table.Name);

                var builder = new TSqlStatementScriptBuilder();

                builder.BuildSelectStatement(table.SelectStatement, false);

                sb.AppendLine(builder.ToString());
            }

            sb.AppendLine(";");

            return sb.ToString();
        }
    }
}