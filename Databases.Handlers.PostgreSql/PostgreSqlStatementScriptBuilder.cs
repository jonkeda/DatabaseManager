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
    public class PostgreSqlStatementScriptBuilder : FullStatementScriptBuilder
    {
        protected override bool IsPostgreBuilder => true;


        public override StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            base.Build(statement, appendSeparator);

            if (statement is IStatementScriptBuilder builder)
            {
                builder.Build(this);
            }
            return this;
        }

        public override void Builds(SelectStatement select)
        {
            BuildSelectStatement(select, true);
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

            if (select.NoTableName && select.Columns.Any(item => AnalyserHelper.IsAssignNameColumn(item)))
            {
                foreach (var column in select.Columns)
                {
                    var symbol = column.Symbol;

                    if (AnalyserHelper.IsAssignNameColumn(column))
                    {
                        var items = symbol.Split('=');

                        var values = items.Skip(1);

                        var strValue = "";

                        if (values.Count() == 1)
                        {
                            strValue = GetSetVariableValue(items[0], items[1]);
                        }
                        else
                        {
                            strValue = string.Join("=", items.Skip(1));
                        }

                        symbol = $"{items[0]}:={strValue}";
                    }

                    AppendLine($"{symbol};");
                }
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

            void AppendWith()
            {
                var i = 0;

                foreach (var withStatement in select.WithStatements)
                {
                    if (i == 0)
                    {
                        AppendLine($"WITH {withStatement.Name}");
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
            }

            void AppendFrom()
            {
                if (select.HasFromItems)
                {
                    BuildSelectStatementFromItems(select);
                }
                else if (select.TableName != null)
                {
                    AppendLine($"FROM {GetNameWithAlias(select.TableName)}");
                }
            }

            if (isWith)
            {
                AppendWith();

                AppendLine(selectColumns);
            }

            AppendFrom();

            if (select.Where != null)
            {
                AppendLine($"WHERE {select.Where}");
            }

            if (select.GroupBy != null && select.GroupBy.Count > 0)
            {
                AppendLine($"GROUP BY {string.Join(",", select.GroupBy)}");
            }

            if (select.Having != null)
            {
                AppendLine($"HAVING {select.Having}");
            }

            if (select.OrderBy != null && select.OrderBy.Count > 0)
            {
                AppendLine($"ORDER BY {string.Join(",", select.OrderBy)}");
            }

            if (select.TopInfo != null)
            {
                AppendLine($"LIMIT {select.TopInfo.TopCount}");
            }

            if (select.LimitInfo != null)
            {
                AppendLine($"LIMIT {select.LimitInfo.RowCount} OFFSET {select.LimitInfo.StartRowIndex?.Symbol ?? "0"}");
            }

            if (select.UnionStatements != null)
            {
                foreach (var union in select.UnionStatements)
                {
                    Build(union, false).TrimSeparator();
                    AppendLine();
                }
            }

            if (isCreateTemporaryTable)
            {
                AppendLine(")");
            }

            if (appendSeparator)
            {
                AppendLine(";");
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
            AppendLine($"RAISE INFO '%',{content};");
        }

        private string GetSetVariableValue(string name, string value)
        {
            if (name != null && value != null && ValueHelper.IsStringValue(value))
            {
                var declareVariable =
                    DeclareVariableStatements.FirstOrDefault(item => item.Name.Symbol?.Trim() == name.Trim());

                var dataType = declareVariable?.DataType?.Symbol?.ToUpper();

                if (dataType != null)
                {
                    if (dataType == "DATE")
                    {
                        value = $"{value}::DATE";
                    }
                    else if (dataType.Contains("TIMESTAMP"))
                    {
                        value = $"{value}::TIMESTAMP";
                    }
                }
            }

            return value;
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

                foreach (var column in columns)
                {
                    var name = column.Name.Symbol;
                    var dataType = column.DataType?.Symbol ?? "";
                    var require = column.IsNullable ? " NULL" : " NOT NULL";
                    var seperator = i == table.Columns.Count - 1 ? hasTableConstraints ? "," : "" : ",";

                    if (column.IsComputed)
                    {
                        sb.AppendLine(
                            $"{name}{dataType}{require} GENERATED ALWAYS AS ({column.ComputeExp}) STORED{seperator}");
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

                if (hasTableConstraints)
                {
                    sb.AppendLine(GetConstraints(table.Constraints));
                }

                sb.AppendLine(")");
            }
            else
            {
                var builder = new PostgreSqlStatementScriptBuilder();

                builder.BuildSelectStatement(selectStatement, false);

                sb.AppendLine(builder.ToString());
            }

            sb.Append(";");

            return sb.ToString();
        }

        #region Statements

        public override void Builds(UnionStatement union)
        {
            AppendLine(GetUnionTypeName(union.Type));
            Build(union.SelectStatement);
        }

        public override void Builds(GotoStatement gts)
        {
            if (gts.IsLabel)
            {
                AppendLine($"--GOTO-- {gts.Label};");
            }
            else
            {
                AppendLine($"--GOTO--{gts.Label}");

                AppendChildStatements(gts.Statements);
            }
        }

        public override void Builds(PreparedStatement prepared)
        {
            var type = prepared.Type;

            if (type == PreparedStatementType.Prepare)
            {
                if (Option.CollectSpecialStatementTypes.Contains(prepared.GetType()))
                {
                    SpecialStatements.Add(prepared);
                }
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

                AppendLine($"EXECUTE {pre?.FromSqlOrVariable}{variables};");
            }
        }

        public override void Builds(RaiseErrorStatement error)
        {
            if (error.Content != null)
            {
                AppendLine($"RAISE EXCEPTION '%',{error.Content};");
            }
        }

        public override void Builds(DropStatement drop)
        {
            var objectType = drop.ObjectType.ToString().ToUpper();

            AppendLine($"DROP {objectType} IF EXISTS {drop.ObjectName.NameWithSchema};");
        }

        public override void Builds(TruncateStatement truncate)
        {
            AppendLine($"TRUNCATE TABLE {truncate.TableName};");
        }

        public override void Builds(CloseCursorStatement closeCursor)
        {
            AppendLine($"CLOSE {closeCursor.CursorName};");
        }

        public override void Builds(FetchCursorStatement fetchCursor)
        {
            if (fetchCursor.Variables.Count > 0)
            {
                AppendLine($"FETCH {fetchCursor.CursorName} INTO {string.Join(",", fetchCursor.Variables)};");
            }
        }

        public override void Builds(OpenCursorStatement openCursor)
        {
            AppendLine($"OPEN {openCursor.CursorName};");
        }

        public override void Builds(DeclareCursorStatement declareCursor)
        {
            var sb = new StringBuilder();

            sb.Append(
                $"DECLARE {declareCursor.CursorName} CURSOR{(declareCursor.SelectStatement != null ? " FOR" : "")}");

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
                {
                    PrintMessage($"'{StringHelper.HandleSingleQuotationChar(sb.ToString())}'");
                }
            }

            if (Option != null && Option.CollectDeclareStatement)
            {
                if (!DeclareCursorStatements.Any(item => item.CursorName.Symbol == declareCursor.CursorName.Symbol))
                {
                    DeclareCursorStatements.Add(declareCursor);
                }
            }
        }

        public override void Builds(TryCatchStatement tryCatch)
        {
            AppendChildStatements(tryCatch.TryStatements);

            AppendLine("EXCEPTION WHEN OTHERS THEN");

            AppendChildStatements(tryCatch.CatchStatements);
        }

        public override void Builds(ExceptionStatement exception)
        {
            AppendLine("EXCEPTION");

            foreach (var exceptionItem in exception.Items)
            {
                var name = exceptionItem.Name?.Symbol;

                AppendLine($"WHEN {name} THEN");

                AppendChildStatements(exceptionItem.Statements);
            }
        }

        public override void Builds(LeaveStatement leave)
        {
            AppendLine("RETURN;");
        }

        public override void Builds(BreakStatement @break)
        {
            AppendLine("EXIT;");
        }

        public override void Builds(TransactionStatement transaction)
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

        public override void Builds(CallStatement call)
        {
            if (!call.IsExecuteSql)
            {
                AppendLine(
                    $"CALL {call.Name}({string.Join(",", call.Parameters.Select(item => item.Value?.Symbol?.Split('=')?.LastOrDefault()))});");
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

                        if (!parameter.IsDescription)
                        {
                            usings.Add(parameter);
                        }
                    }

                    var strUsings = usings.Count == 0
                        ? ""
                        : $" USING {string.Join(",", usings.Select(item => $"{item.Value}"))}";

                    AppendLine($"EXECUTE {content}{strUsings};");
                }
            }
        }

        public override void Builds(ReturnStatement @return)
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

                AppendLine("RETURN;");
            }
        }

        public override void Builds(WhileStatement @while)
        {
            AppendLine($"WHILE {@while.Condition} LOOP");
            AppendChildStatements(@while.Statements);
            AppendLine("END LOOP;");
        }

        public override void Builds(LoopExitStatement loopExit)
        {
            if (!loopExit.IsCursorLoopExit)
            {
                AppendLine($"EXIT WHEN {loopExit.Condition};");
            }
            else
            {
                AppendLine("EXIT WHEN NOT FOUND;");
            }
        }

        public override void Builds(LoopStatement loop)
        {
            var isReverse = false;
            var isForLoop = false;
            var isIntegerIterate = false;
            string iteratorName = null;

            if (loop.Type == LoopType.LOOP)
            {
                AppendLine("LOOP");
            }
            else if (loop.Type == LoopType.FOR && loop.LoopCursorInfo != null)
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
                        DataType = new TokenInfo("INTEGER")
                    };

                    DeclareVariableStatements.Add(declareVariable);

                    if (!isReverse)
                    {
                        AppendLine($"{iteratorName}:={loop.LoopCursorInfo.StartValue};");
                        AppendLine($"WHILE {iteratorName}<={loop.LoopCursorInfo.StopValue} LOOP");
                    }
                    else
                    {
                        AppendLine($"{iteratorName}:={loop.LoopCursorInfo.StopValue};");
                        AppendLine($"WHILE {iteratorName}>={loop.LoopCursorInfo.StartValue} LOOP");
                    }
                }
            }
            else
            {
                AppendLine($"{loop.Type.ToString()} {loop.Condition} LOOP");
            }

            AppendChildStatements(loop.Statements);

            if (isForLoop && isIntegerIterate)
            {
                AppendLine($"{iteratorName}:={iteratorName}{(isReverse ? "-" : "+")}1;");
            }

            AppendLine("END LOOP;");
        }

        public override void Builds(SetStatement set)
        {
            if (set.Key != null)
            {
                if (set.Value != null)
                {
                    if (set.IsSetUserVariable)
                    {
                        var dataType =
                            AnalyserHelper.GetUserVariableDataType(DatabaseType.Postgres, set.UserVariableDataType);

                        if (!string.IsNullOrEmpty(dataType))
                        {
                            var declareVariable = new DeclareVariableStatement
                                { Name = set.Key, DataType = new TokenInfo(dataType) };

                            DeclareVariableStatements.Add(declareVariable);
                        }
                    }

                    var value = GetSetVariableValue(set.Key.Symbol, set.Value.Symbol);

                    AppendLine($"{set.Key} := {value};");
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

        public override void Builds(CaseStatement @case)
        {
            AppendLine($"CASE {@case.VariableName}");

            foreach (var item in @case.Items)
            {
                if (item.Type != IfStatementType.ELSE)
                {
                    AppendLine($"WHEN {item.Condition} THEN");
                }
                else
                {
                    AppendLine("ELSE");
                }

                AppendLine("BEGIN");
                AppendChildStatements(item.Statements);
                AppendLine("END;");
            }

            AppendLine("END CASE;");
        }

        public override void Builds(IfStatement @if)
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

        public override void Builds(CreateTableStatement createTable)
        {
            AppendLine(BuildTable(createTable.TableInfo));
        }

        public override void Builds(DeclareTableStatement declareTable)
        {
            AppendLine(BuildTable(declareTable.TableInfo));
        }

        public override void Builds(DeclareVariableStatement declareVar)
        {
            var sb = new StringBuilder();

            var defaultValue = declareVar.DefaultValue == null ? "" : $" DEFAULT {declareVar.DefaultValue}";

            sb.Append($"DECLARE {declareVar.Name} {declareVar.DataType}{defaultValue};");

            if (!(Option != null && Option.NotBuildDeclareStatement))
            {
                AppendLine(sb.ToString());
            }
            else
            {
                if (Option.OutputRemindInformation)
                {
                    PrintMessage($"'{StringHelper.HandleSingleQuotationChar(sb.ToString())}'");
                }
            }

            if (Option != null && Option.CollectDeclareStatement)
            {
                DeclareVariableStatements.Add(declareVar);
            }
        }

        public override void Builds(DeleteStatement delete)
        {
            var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(delete.FromItems);

            if (!hasJoin)
            {
                AppendLine($"DELETE FROM {GetNameWithAlias(delete.TableName)}");
            }
            else
            {
                Append("DELETE");

                BuildFromItems(delete.FromItems, null, true);
            }

            if (delete.Condition != null)
            {
                AppendLine($"{(hasJoin ? "AND" : "WHERE")} {delete.Condition}");
            }

            AppendLine(";");
        }

        public override void Builds(UpdateStatement update)
        {
            var fromItemsCount = update.FromItems == null ? 0 : update.FromItems.Count;
            var tableName = StatementScriptBuilderHelper.GetUpdateSetTableName(update);
            var tableNameOrAlias = tableName?.Symbol;
            var isCompositeColumnName = StatementScriptBuilderHelper.IsCompositeUpdateSetColumnName(update);

            AppendLine("UPDATE");

            var tableNames = new List<TableName>();

            var joins = new List<string>();
            string otherCondition = null;
            string strTableName = null;
            string alias = null;

            if (tableName?.Alias != null)
            {
                alias = tableName.Alias.Symbol;
            }

            string GetNoAliasString(string str, bool useOldName)
            {
                if (str != null)
                {
                    return alias == null ? str : str.Replace($"{alias}.", useOldName ? $"{strTableName}." : "");
                }

                return str;
            }

            if (update.HasFromItems)
            {
                var i = 0;

                foreach (var fromItem in update.FromItems)
                {
                    var hasJoin = fromItem.HasJoinItems;

                    var tn = fromItem.TableName?.Symbol;
                    var talias = fromItem.TableName?.Alias?.ToString();

                    if (fromItem.TableName != null)
                    {
                        var matched = false;

                        if (alias != null)
                        {
                            if (talias == alias)
                            {
                                matched = true;
                            }
                        }
                        else if (tn == tableNameOrAlias)
                        {
                            matched = true;
                        }

                        if (matched)
                        {
                            tableNames.Add(fromItem.TableName);
                            strTableName = tn;
                            alias = talias;
                        }
                    }

                    if (hasJoin)
                    {
                        var j = 0;

                        foreach (var joinItem in fromItem.JoinItems)
                        {
                            if (j == 0)
                            {
                                joins.Add($"FROM {joinItem.TableName.NameWithAlias}");
                                otherCondition = GetNoAliasString(joinItem.Condition.ToString(), true);
                            }
                            else
                            {
                                joins.Add(
                                    $"{joinItem.Type} JOIN {joinItem.TableName.NameWithAlias} ON {GetNoAliasString(joinItem.Condition.ToString(), false)}");
                            }

                            j++;
                        }
                    }
                    else if (fromItemsCount > 0)
                    {
                        var seperator = joins.Count < fromItemsCount - 2 && fromItemsCount > 2 ? "," : "";

                        if (tn != null)
                        {
                            if (tn != strTableName || (tn == strTableName && alias != talias))
                            {
                                joins.Add(
                                    $"{(joins.Count == 0 ? "FROM" : "")} {fromItem.TableName.NameWithAlias}{seperator}");
                            }
                        }
                        else if (fromItem.SubSelectStatement != null)
                        {
                            var builder = new PostgreSqlStatementScriptBuilder();

                            if (joins.Count == 0)
                            {
                                builder.Append("FROM ");
                            }

                            var strAlias = fromItem.Alias == null ? "" : fromItem.Alias.Symbol;

                            builder.AppendLine("(");
                            builder.BuildSelectStatement(fromItem.SubSelectStatement, false);
                            builder.Append($") {strAlias}");

                            builder.Append(seperator);

                            joins.Add(builder.ToString());
                        }
                    }

                    i++;
                }
            }

            if (tableNames.Count == 0 && update.TableNames.Count > 0)
            {
                tableNames.AddRange(update.TableNames);

                if (strTableName == null)
                {
                    strTableName = update.TableNames.FirstOrDefault()?.Symbol;
                }
            }

            Append($" {string.Join(",", tableNames.Select(item => item.NameWithAlias))}", false);

            AppendLine("SET");

            if (!isCompositeColumnName)
            {
                var k = 0;

                foreach (var item in update.SetItems)
                {
                    Append($"{item.Name}=");

                    BuildUpdateSetValue(item);

                    if (k < update.SetItems.Count - 1)
                    {
                        Append(",");
                    }

                    AppendLine(Indent);

                    k++;
                }

                joins.ForEach(item => AppendLine(item));
            }
            else
            {
                AppendLine(StatementScriptBuilderHelper.ParseCompositeUpdateSet(DatabaseType.Postgres, update));
                return; // true;
            }

            var hasCondition = false;

            if (update.Condition != null && update.Condition.Symbol != null)
            {
                AppendLine($"WHERE {update.Condition}");
                hasCondition = true;
            }

            if (otherCondition != null)
            {
                AppendLine(hasCondition ? $"AND {otherCondition}" : $"WHERE {otherCondition}");
            }

            AppendLine(";");
        }

        public override void Builds(InsertStatement insert)
        {
            AppendLine($"INSERT INTO {insert.TableName}");

            if (insert.Columns.Count > 0)
            {
                AppendLine($"({string.Join(",", insert.Columns.Select(item => item.ToString()))})");
            }

            if (insert.SelectStatements != null && insert.SelectStatements.Count > 0)
            {
                AppendChildStatements(insert.SelectStatements);
            }
            else
            {
                AppendLine($"VALUES({string.Join(",", insert.Values.Select(item => item))});");
            }
        }

        public override void Builds(DeallocateCursorStatement deallocateCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(PrintStatement print)
        {
            throw new NotImplementedException();
        }

        public override void Builds(ContinueStatement @continue)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeclareCursorHandlerStatement declareCursorHandler)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}