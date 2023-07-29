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
    public class MySqlStatementScriptBuilder : StatementScriptBuilder
    {
        protected override void PreHandleStatements(List<Statement> statements)
        {
            base.PreHandleStatements(statements);

            MySqlAnalyserHelper.RearrangeStatements(statements);
        }

        public override StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            base.Build(statement, appendSeparator);

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
                Append($"INSERT INTO {insert.TableName}");

                if (insert.Columns.Count > 0) AppendLine($"({string.Join(",", insert.Columns.Select(item => item))})");

                if (insert.SelectStatements != null && insert.SelectStatements.Count > 0)
                    AppendChildStatements(insert.SelectStatements);
                else
                    AppendLine($"VALUES({string.Join(",", insert.Values.Select(item => item))});");
            }
            else if (statement is UpdateStatement update)
            {
                var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(update.FromItems);
                var fromItemsCount = update.FromItems == null ? 0 : update.FromItems.Count;

                var isCompositeColumnName = StatementScriptBuilderHelper.IsCompositeUpdateSetColumnName(update);

                Append("UPDATE");

                if (isCompositeColumnName)
                {
                    Append(StatementScriptBuilderHelper.ParseCompositeUpdateSet(DatabaseType.MySql, update));

                    return this;
                }

                var tableNames = new List<TableName>();

                if (fromItemsCount > 0 && update.FromItems.First().TableName != null)
                    tableNames.Add(update.FromItems.First().TableName);
                else if (update.TableNames.Count > 0) tableNames.AddRange(update.TableNames);

                Append(
                    $" {string.Join(",", tableNames.Where(item => item != null).Select(item => item.NameWithAlias))}");

                if (!hasJoin)
                {
                    if (fromItemsCount > 0)
                        for (var i = 0; i < fromItemsCount; i++)
                        {
                            var fromItem = update.FromItems[i];
                            var tableName = fromItem.TableName;

                            if (tableName != null && !tableNames.Contains(tableName))
                            {
                                Append($",{tableName.NameWithAlias}");
                            }
                            else if (fromItem.SubSelectStatement != null)
                            {
                                var alias = fromItem.Alias == null ? "" : fromItem.Alias.Symbol;

                                Append(",");
                                AppendLine("(");
                                BuildSelectStatement(fromItem.SubSelectStatement, false);
                                AppendLine($") {alias}");
                            }
                        }
                }
                else
                {
                    var i = 0;

                    foreach (var fromItem in update.FromItems)
                    {
                        if (fromItem.TableName != null && i > 0) AppendLine($" {fromItem.TableName}");

                        foreach (var joinItem in fromItem.JoinItems)
                        {
                            var condition = joinItem.Condition == null ? "" : $" ON {joinItem.Condition}";

                            AppendLine($"{joinItem.Type} JOIN {GetNameWithAlias(joinItem.TableName)}{condition}");
                        }

                        i++;
                    }
                }

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
                    AppendLine($"WHERE {update.Condition}");

                AppendLine(";");
            }
            else if (statement is DeleteStatement delete)
            {
                var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(delete.FromItems);

                if (!hasJoin)
                {
                    AppendLine($"DELETE FROM {GetNameWithAlias(delete.TableName)}");
                }
                else
                {
                    var tableName = delete.TableName.Symbol;

                    string alias = null;

                    var firstFromItem = delete.FromItems[0];

                    if (firstFromItem.TableName != null && firstFromItem.TableName.Alias != null)
                        alias = firstFromItem.TableName.Alias.Symbol;
                    else if (firstFromItem.Alias != null) alias = firstFromItem.Alias.Symbol;

                    AppendLine($"DELETE {(string.IsNullOrEmpty(alias) ? delete.TableName.Symbol : alias)}");

                    BuildFromItems(delete.FromItems);
                }

                if (delete.Condition != null) AppendLine($"WHERE {delete.Condition}");

                AppendLine(";");
            }
            else if (statement is DeclareVariableStatement declareVar)
            {
                if (!(Option != null && Option.NotBuildDeclareStatement))
                {
                    var defaultValue = declareVar.DefaultValue == null ? "" : $" DEFAULT {declareVar.DefaultValue}";
                    AppendLine($"DECLARE {declareVar.Name} {declareVar.DataType}{defaultValue};");
                }

                if (Option != null && Option.CollectDeclareStatement) DeclareVariableStatements.Add(declareVar);
            }
            else if (statement is DeclareTableStatement declareTable)
            {
                AppendLine(BuildTable(declareTable.TableInfo));
            }
            else if (statement is CreateTableStatement createTable)
            {
                AppendLine(BuildTable(createTable.TableInfo));
            }
            else if (statement is IfStatement @if)
            {
                foreach (var item in @if.Items)
                {
                    if (item.Type == IfStatementType.IF || item.Type == IfStatementType.ELSEIF)
                    {
                        Append($"{item.Type} ");

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
                        AppendLine($"SET {set.Key} = {set.Value};");
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
                var name = loop.Name;
                var isReverse = false;
                var isForLoop = false;
                var isIntegerIterate = false;
                string iteratorName = null;

                if (loop.Type != LoopType.LOOP)
                {
                    var hasExitStatement = AnalyserHelper.HasExitStatement(loop);
                    var label = hasExitStatement ? GetNextLoopLabel("w") : "";

                    if (loop.Condition == null)
                    {
                        if (loop.Type != LoopType.FOR)
                        {
                            AppendLine($"{label}WHILE 1=1 DO");
                        }
                        else if (loop.LoopCursorInfo != null)
                        {
                            isForLoop = true;
                            isReverse = loop.LoopCursorInfo.IsReverse;
                            iteratorName = loop.LoopCursorInfo.IteratorName.Symbol;

                            if (loop.LoopCursorInfo.IsIntegerIterate)
                            {
                                isIntegerIterate = true;

                                var declareVariable = new DeclareVariableStatement
                                {
                                    Name = loop.LoopCursorInfo.IteratorName,
                                    DataType = new TokenInfo("INT")
                                };

                                DeclareVariableStatements.Add(declareVariable);

                                if (!isReverse)
                                {
                                    AppendLine($"SET {iteratorName}={loop.LoopCursorInfo.StartValue};");
                                    AppendLine($"WHILE {iteratorName}<={loop.LoopCursorInfo.StopValue} DO");
                                }
                                else
                                {
                                    AppendLine($"SET {iteratorName}={loop.LoopCursorInfo.StopValue};");
                                    AppendLine($"WHILE {iteratorName}>={loop.LoopCursorInfo.StartValue} DO");
                                }
                            }
                        }
                    }
                    else
                    {
                        AppendLine($"{label}WHILE {loop.Condition} DO");
                    }
                }
                else
                {
                    AppendLine("LOOP");
                }

                AppendLine("BEGIN");

                AppendChildStatements(loop.Statements);

                if (isForLoop && isIntegerIterate)
                    AppendLine($"SET {iteratorName}= {iteratorName}{(isReverse ? "-" : "+")}1;");

                AppendLine("END;");

                if (loop.Type != LoopType.LOOP)
                    AppendLine("END WHILE;");
                else
                    AppendLine($"END LOOP {(name == null ? "" : name + ":")};");
            }
            else if (statement is WhileStatement @while)
            {
                var hasExitStatement = AnalyserHelper.HasExitStatement(@while);
                var label = hasExitStatement ? GetNextLoopLabel("w") : "";

                AppendLine($"{label}WHILE {@while.Condition} DO");

                AppendChildStatements(@while.Statements);

                AppendLine("END WHILE;");
            }
            else if (statement is LoopExitStatement whileExit)
            {
                if (!whileExit.IsCursorLoopExit)
                {
                    AppendLine($"IF {whileExit.Condition} THEN");
                    AppendLine("BEGIN");
                    AppendLine($"LEAVE {GetCurrentLoopLabel("w")};");
                    AppendLine("END;");
                    AppendLine("END IF;");
                }
            }
            else if (statement is BreakStatement @break)
            {
                AppendLine($"LEAVE {GetCurrentLoopLabel("w")};");
            }
            else if (statement is ReturnStatement @return)
            {
                var value = @return.Value?.Symbol;

                if (RoutineType != RoutineType.PROCEDURE)
                {
                    AppendLine($"RETURN {value};");
                }
                else
                {
                    var isStringValue = ValueHelper.IsStringValue(value);

                    PrintMessage(isStringValue ? StringHelper.HandleSingleQuotationChar(value) : value);

                    //Use it will cause syntax error.
                    //this.AppendLine("RETURN;");
                }
            }
            else if (statement is PrintStatement print)
            {
                PrintMessage(print.Content?.Symbol);
            }
            else if (statement is CallStatement call)
            {
                if (!call.IsExecuteSql)
                {
                    var content = string.Join(",",
                        call.Parameters.Select(item => item.Value?.Symbol?.Split('=')?.LastOrDefault()));

                    AppendLine($"CALL {call.Name}({content});");
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
                            : $" USING {string.Join(",", usings.Select(item => $"@{item.Value}"))}";

                        AppendLine($"SET @SQL:={content};");
                        AppendLine("PREPARE dynamicSQL FROM @SQL;");
                        AppendLine($"EXECUTE dynamicSQL{strUsings};");
                        AppendLine("DEALLOCATE PREPARE dynamicSQL;");
                        AppendLine();
                    }
                }
            }
            else if (statement is TransactionStatement transaction)
            {
                var commandType = transaction.CommandType;

                switch (commandType)
                {
                    case TransactionCommandType.BEGIN:
                        AppendLine("START TRANSACTION;");
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
                AppendLine("LEAVE sp;");

                if (Option.CollectSpecialStatementTypes.Contains(leave.GetType())) SpecialStatements.Add(leave);
            }
            else if (statement is TryCatchStatement tryCatch)
            {
                if (!(Option != null && Option.NotBuildDeclareStatement))
                {
                    AppendLine("DECLARE EXIT HANDLER FOR 1 #[REPLACE ERROR CODE HERE]");
                    AppendLine("BEGIN");

                    AppendChildStatements(tryCatch.CatchStatements);

                    AppendLine("END;");
                }

                AppendChildStatements(tryCatch.TryStatements);

                if (Option != null && Option.CollectDeclareStatement) OtherDeclareStatements.Add(tryCatch);
            }
            else if (statement is ExceptionStatement exception)
            {
                if (!(Option != null && Option.NotBuildDeclareStatement))
                    foreach (var exceptionItem in exception.Items)
                    {
                        AppendLine($"DECLARE EXIT HANDLER FOR {exceptionItem.Name}");
                        AppendLine("BEGIN");

                        AppendChildStatements(exceptionItem.Statements);

                        AppendLine("END;");
                    }

                if (Option != null && Option.CollectDeclareStatement) OtherDeclareStatements.Add(exception);
            }
            else if (statement is DeclareCursorStatement declareCursor)
            {
                if (!(Option != null && Option.NotBuildDeclareStatement))
                    if (declareCursor.SelectStatement != null)
                    {
                        AppendLine($"DECLARE {declareCursor.CursorName} CURSOR FOR");

                        BuildSelectStatement(declareCursor.SelectStatement);
                    }

                if (Option != null && Option.CollectDeclareStatement)
                    if (!DeclareCursorStatements.Any(item => item.CursorName.Symbol == declareCursor.CursorName.Symbol))
                        DeclareCursorStatements.Add(declareCursor);
            }
            else if (statement is DeclareCursorHandlerStatement declareCursorHandler)
            {
                if (!(Option != null && Option.NotBuildDeclareStatement))
                {
                    AppendLine("DECLARE CONTINUE HANDLER");
                    AppendLine("FOR NOT FOUND");
                    AppendLine("BEGIN");
                    AppendChildStatements(declareCursorHandler.Statements);
                    AppendLine("END;");
                }

                if (Option != null && Option.CollectDeclareStatement) OtherDeclareStatements.Add(declareCursorHandler);
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
            }
            else if (statement is DropStatement drop)
            {
                var objectType = drop.ObjectType.ToString().ToUpper();

                AppendLine(
                    $"DROP {(drop.IsTemporaryTable ? "TEMPORARY" : "")} {objectType} IF EXISTS {drop.ObjectName.NameWithSchema};");
            }
            else if (statement is RaiseErrorStatement error)
            {
                //https://dev.mysql.com/doc/refman/8.0/en/signal.html
                var code = error.ErrorCode == null ? "45000" : error.ErrorCode.Symbol;

                AppendLine($"SIGNAL SQLSTATE '{code}' SET MESSAGE_TEXT={error.Content};");
            }
            else if (statement is PreparedStatement prepared)
            {
                var type = prepared.Type;

                if (type == PreparedStatementType.Prepare)
                {
                    AppendLine($"PREPARE {prepared.Id} FROM {prepared.FromSqlOrVariable};");
                }
                else if (type == PreparedStatementType.Execute)
                {
                    var usingVariables = prepared.ExecuteVariables.Count > 0
                        ? $" USING {string.Join(",", prepared.ExecuteVariables)}"
                        : "";

                    AppendLine($"EXECUTE {prepared.Id}{usingVariables};");
                }
                else if (type == PreparedStatementType.Deallocate)
                {
                    AppendLine($"DEALLOCATE PREPARE {prepared.Id};");
                }
            }
            else if (statement is GotoStatement gts)
            {
                if (gts.IsLabel)
                {
                    AppendLine($"#GOTO {gts.Label};");
                }
                else
                {
                    AppendLine($"#GOTO#{gts.Label}");

                    AppendChildStatements(gts.Statements);
                }
            }

            return this;
        }

        protected override void BuildSelectStatement(SelectStatement select, bool appendSeparator = true)
        {
            var isCreateTemporaryTable = false;

            var intoTableName = AnalyserHelper.GetIntoTableName(select);

            if (intoTableName != null)
            {
                isCreateTemporaryTable = true;

                AppendLine($"CREATE TEMPORARY TABLE IF NOT EXISTS {intoTableName} AS (");
            }

            var isWith = select.WithStatements != null && select.WithStatements.Count > 0;

            var selectColumns = $"SELECT {string.Join(",", select.Columns.Select(item => GetNameWithAlias(item)))}";
            var hasAssignVariableColumn = HasAssignVariableColumn(select);

            if (select.NoTableName && select.Columns.Count == 1 && hasAssignVariableColumn)
            {
                var columnName = select.Columns.First();

                AppendLine($"SET {columnName}");
            }
            else if (!select.NoTableName && hasAssignVariableColumn &&
                     (RoutineType == RoutineType.FUNCTION || RoutineType == RoutineType.TRIGGER))
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
                AppendLine($"GROUP BY {string.Join(",", select.GroupBy.Select(item => item))}");

            if (select.Having != null) AppendLine($"HAVING {select.Having}");

            if (select.OrderBy != null && select.OrderBy.Count > 0)
                AppendLine($"ORDER BY {string.Join(",", select.OrderBy.Select(item => item))}");

            if (select.TopInfo != null) AppendLine($"LIMIT 0,{select.TopInfo.TopCount}");

            if (select.LimitInfo != null)
                AppendLine($"LIMIT {select.LimitInfo.StartRowIndex?.Symbol ?? "0"},{select.LimitInfo.RowCount}");

            if (select.UnionStatements != null)
                foreach (var union in select.UnionStatements)
                {
                    Build(union, false).TrimSeparator();
                    AppendLine();
                }

            if (isCreateTemporaryTable) AppendLine(")");

            if (appendSeparator) AppendLine(";", false);
        }

        private string GetUnionTypeName(UnionType unionType)
        {
            switch (unionType)
            {
                case UnionType.UNION_ALL:
                    return "UNION ALL";
                default:
                    return unionType.ToString();
            }
        }

        private void PrintMessage(string content)
        {
            AppendLine($"SELECT {content};");
        }

        public string BuildTable(TableInfo table)
        {
            var sb = new StringBuilder();

            var columns = table.Columns;
            var selectStatement = table.SelectStatement;

            sb.AppendLine(
                $"CREATE {(table.IsTemporary ? "TEMPORARY" : "")} TABLE {table.Name}{(columns.Count > 0 ? "(" : "AS")}");

            if (columns.Count > 0)
            {
                var hasTableConstraints = table.HasTableConstraints;

                var i = 0;

                var primaryKeyUsed = false;
                string primaryKeyColumn = null;

                var tablePrimaryKey = table.Constraints.Where(item => item.Type == ConstraintType.PrimaryKey)
                    ?.FirstOrDefault();
                var primaryContraintsColumns = table.Constraints == null
                    ? Enumerable.Empty<ColumnName>()
                    : tablePrimaryKey?.ColumnNames;

                foreach (var column in columns)
                {
                    var name = column.Name.Symbol;
                    var dataType = column.DataType?.Symbol ?? "";
                    var require = column.IsNullable ? " NULL" : " NOT NULL";
                    var seperator = i == table.Columns.Count - 1 ? hasTableConstraints ? "," : "" : ",";

                    var isComputeExp = column.IsComputed;

                    if (isComputeExp)
                    {
                        sb.AppendLine($"{name} {dataType} AS ({column.ComputeExp}){require}{seperator}");
                    }
                    else
                    {
                        var identity = column.IsIdentity ? " AUTO_INCREMENT" : "";
                        var defaultValue = string.IsNullOrEmpty(column.DefaultValue?.Symbol)
                            ? ""
                            : $" DEFAULT {StringHelper.GetParenthesisedString(column.DefaultValue.Symbol)}";
                        var constraint = GetConstraints(column.Constraints, true);
                        var strConstraint = string.IsNullOrEmpty(constraint) ? "" : $" {constraint}";

                        if (column.IsIdentity && !strConstraint.Contains("PRIMARY"))
                        {
                            if (primaryContraintsColumns != null && primaryContraintsColumns.Count() == 1 &&
                                primaryContraintsColumns.First().Symbol == name)
                            {
                                strConstraint += " PRIMARY KEY";
                                primaryKeyColumn = name;

                                primaryKeyUsed = true;
                            }
                            else if (primaryContraintsColumns != null &&
                                     !primaryContraintsColumns.Any(item => item.Symbol == name))
                            {
                                tablePrimaryKey.ColumnNames.Insert(0, column.Name);
                            }
                        }

                        sb.AppendLine(
                            $"{name} {column.DataType}{identity}{require}{defaultValue}{strConstraint}{seperator}");
                    }

                    i++;
                }

                if (hasTableConstraints)
                {
                    var tableConstraints = table.Constraints;

                    if (primaryKeyUsed)
                        tableConstraints = tableConstraints.Where(item =>
                            !(item.Type == ConstraintType.PrimaryKey &&
                              item.ColumnNames.Any(t => t.Symbol == primaryKeyColumn))).ToList();

                    sb.AppendLine(GetConstraints(tableConstraints));
                }

                sb.Append(")");
            }
            else
            {
                var builder = new MySqlStatementScriptBuilder();

                builder.BuildSelectStatement(selectStatement, false);

                sb.AppendLine(builder.ToString());
            }

            sb.AppendLine(";");

            return sb.ToString();
        }

        protected override void AddConstraintDefinition(bool isForColumn, string name, StringBuilder sb,
            string definition)
        {
            sb.Append($" {definition}");
        }
    }
}