using System;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Databases.SqlAnalyser;
using Databases.SqlAnalyser.Model.DatabaseObject;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Statement.Cursor;
using Databases.SqlAnalyser.Model.Token;

namespace SqlAnalyser.Core
{
    public class SqliteStatementScriptBuilder : FullStatementScriptBuilder
    {
        public override StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            if (statement is IStatementScriptBuilder builder)
            {
                builder.Build(this);
            }
            else if (statement is SelectStatement select)
            {
                BuildSelectStatement(select, appendSeparator);
            }

            return this;
        }

        public override void Builds(RaiseErrorStatement error)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DropStatement drop)
        {
            var objectType = drop.ObjectType.ToString().ToUpper();

            AppendLine($"DROP {objectType} IF EXISTS {drop.ObjectName.NameWithSchema};");
        }

        public override void Builds(TruncateStatement truncate)
        {
            AppendLine($"DELETE FROM {truncate.TableName};");
        }

        public override void Builds(CloseCursorStatement closeCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(FetchCursorStatement fetchCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(OpenCursorStatement openCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeclareCursorStatement declareCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(TryCatchStatement tryCatch)
        {
            throw new NotImplementedException();
        }

        public override void Builds(ExceptionStatement exception)
        {
            throw new NotImplementedException();
        }

        public override void Builds(LeaveStatement leave)
        {
            throw new NotImplementedException();
        }

        public override void Builds(BreakStatement @break)
        {
            throw new NotImplementedException();
        }

        public override void Builds(TransactionStatement transaction)
        {
            throw new NotImplementedException();
        }

        public override void Builds(CallStatement call)
        {
            throw new NotImplementedException();
        }

        public override void Builds(ReturnStatement @return)
        {
            throw new NotImplementedException();
        }

        public override void Builds(WhileStatement @while)
        {
            throw new NotImplementedException();
        }

        public override void Builds(LoopExitStatement loopExit)
        {
            throw new NotImplementedException();
        }

        public override void Builds(LoopStatement loop)
        {
            throw new NotImplementedException();
        }

        public override void Builds(SetStatement set)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeallocateCursorStatement deallocateCursor)
        {
            throw new NotImplementedException();
        }

        public override void Builds(PrintStatement print)
        {
            AppendLine($"PRINT {print.Content.Symbol?.Replace("||", "+")};");
        }

        public override void Builds(ContinueStatement @continue)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeclareCursorHandlerStatement declareCursorHandler)
        {
            throw new NotImplementedException();
        }

        public override void Builds(CaseStatement @case)
        {
            var variableName = @case.VariableName.ToString();

            var ifStatement = new IfStatement();

            var i = 0;
            foreach (var item in @case.Items)
            {
                var ifItem = new IfStatementItem
                {
                    Type = i == 0 ? IfStatementType.IF : item.Type
                };

                if (item.Type != IfStatementType.ELSE)
                {
                    ifItem.Condition = new TokenInfo($"{variableName}={item.Condition}")
                        { Type = TokenType.IfCondition };
                }

                i++;
            }

            Build(ifStatement);
        }

        public override void Builds(IfStatement @if)
        {
            throw new NotImplementedException();
        }

        public override void Builds(CreateTableStatement createTable)
        {
            AppendLine(BuildTable(createTable.TableInfo));
        }

        public override void Builds(DeclareTableStatement declareTable)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeclareVariableStatement declareVar)
        {
            throw new NotImplementedException();
        }

        public override void Builds(DeleteStatement delete)
        {
            var hasJoin = AnalyserHelper.IsFromItemsHaveJoin(delete.FromItems);

            if (!hasJoin)
            {
                AppendLine($"DELETE FROM {GetNameWithAlias(delete.TableName)}");

                if (delete.Condition != null)
                {
                    AppendLine($"WHERE {delete.Condition}");
                }
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
                        {
                            alias = fromItem.TableName.Alias.Symbol;
                        }
                        else if (fromItem.Alias != null)
                        {
                            alias = fromItem.Alias.Symbol;
                        }
                    }

                    i++;
                }

                //use placeholder, the actual column name should according to the business needs.
                AppendLine("WHERE $columnName$ IN (SELECT $columnName$");

                BuildFromItems(delete.FromItems, null, true);

                if (delete.Condition != null)
                {
                    AppendLine($"WHERE {delete.Condition}");
                }

                AppendLine(")");
            }

            AppendLine(";");
        }

        public override void Builds(UpdateStatement update)
        {
            var fromItemsCount = update.FromItems == null ? 0 : update.FromItems.Count;
            var tableName = StatementScriptBuilderHelper.GetUpdateSetTableName(update);

            if (tableName == null && update.TableNames != null)
            {
                tableName = update.TableNames.FirstOrDefault();
            }

            Func<TokenInfo, string> getCleanColumnName = token => { return token.Symbol.Split('.').LastOrDefault(); };

            var useAlias = false;

            AppendLine($"UPDATE {(useAlias ? tableName.Alias.Symbol : tableName.Symbol)}");

            Append("SET ");

            if (!StatementScriptBuilderHelper.IsCompositeUpdateSetColumnName(update))
            {
                var k = 0;

                foreach (var item in update.SetItems)
                {
                    var cleanColumnName = getCleanColumnName(item.Name);

                    Append($"{cleanColumnName}=");

                    BuildUpdateSetValue(item);

                    if (k < update.SetItems.Count - 1)
                    {
                        Append(",");
                    }

                    AppendLine(Indent);

                    k++;
                }
            }
            else if (update.SetItems.Count > 0)
            {
                Append(StatementScriptBuilderHelper.ParseCompositeUpdateSet(DatabaseType.Sqlite, update));

                return;
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
                        if (i == 0)
                        {
                            AppendLine($"FROM {fromItem.TableName.NameWithAlias}");
                        }

                        foreach (var joinItem in fromItem.JoinItems)
                        {
                            var condition = joinItem.Condition == null ? "" : $" ON {joinItem.Condition}";

                            AppendLine($"{joinItem.Type} JOIN {joinItem.TableName.NameWithAlias}{condition}");
                        }
                    }
                    else
                    {
                        if (i == 0)
                        {
                            Append("FROM ");
                        }
                        else
                        {
                            Append(",");
                        }

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
                if (useAlias)
                {
                    AppendLine($"FROM {tableName.NameWithAlias}");
                }
            }

            if (update.Condition != null && update.Condition.Symbol != null)
            {
                AppendLine($"WHERE {update.Condition}");
            }

            if (update.Option != null)
            {
                AppendLine(update.Option.ToString());
            }

            AppendLine(";");
        }

        public override void Builds(UnionStatement union)
        {
            AppendLine(GetUnionTypeName(union.Type));
            Build(union.SelectStatement);
        }

        public override void Builds(GotoStatement gts)
        {
            throw new NotImplementedException();
        }

        public override void Builds(PreparedStatement prepared)
        {
            throw new NotImplementedException();
        }

        public override void Builds(InsertStatement insert)
        {
            Append($"INSERT INTO {insert.TableName}", false);

            if (insert.Columns.Count > 0)
            {
                AppendLine($"({string.Join(",", insert.Columns.Select(item => item))})");
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

            if (!isWith)
            {
                Append(selectColumns);
            }

            if (intoTableName != null)
            {
                AppendLine($"INTO {intoTableName}");
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
                        {
                            AppendLine($"({string.Join(",", withStatement.Columns.Select(item => item))})");
                        }
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
                {
                    BuildSelectStatementFromItems(select);
                }
                else if (select.TableName != null)
                {
                    AppendLine($"FROM {GetNameWithAlias(select.TableName)}");
                }
            };

            if (isWith)
            {
                appendWith();

                AppendLine(selectColumns);
            }

            appendFrom();

            if (select.Where != null)
            {
                Append($"WHERE {select.Where}");
            }

            if (select.GroupBy != null && select.GroupBy.Count > 0)
            {
                AppendLine($"GROUP BY {string.Join(",", select.GroupBy.Select(item => item))}");
            }

            if (select.Having != null)
            {
                AppendLine($"HAVING {select.Having}");
            }

            if (select.OrderBy != null && select.OrderBy.Count > 0)
            {
                AppendLine($"ORDER BY {string.Join(",", select.OrderBy.Select(item => item))}");
            }

            if (select.TopInfo != null)
            {
                AppendLine($"LIMIT 0,{select.TopInfo.TopCount}");
            }

            if (select.LimitInfo != null)
            {
                AppendLine($"LIMIT {select.LimitInfo.StartRowIndex?.Symbol ?? "0"},{select.LimitInfo.RowCount}");
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

        private void MakeupRoutineName(TokenInfo token)
        {
            var symbol = token.Symbol;
            var index = symbol.IndexOf("(", StringComparison.Ordinal);

            var name = index == -1 ? symbol : symbol.Substring(0, index);

            if (!name.Contains("."))
            {
                token.Symbol = "dbo." + symbol;
            }
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

                    var isComputeExp = column.IsComputed;

                    if (isComputeExp)
                    {
                        sb.AppendLine(
                            $"{name} {dataType} GENERATED ALWAYS AS ({column.ComputeExp}) STORED{require}{seperator}");
                    }
                    else
                    {
                        var identity = column.IsIdentity ? " AUTO_INCREMENT" : "";
                        var defaultValue = string.IsNullOrEmpty(column.DefaultValue?.Symbol)
                            ? ""
                            : $" DEFAULT {StringHelper.GetParenthesisedString(column.DefaultValue.Symbol)}";
                        var constraint = GetConstraints(column.Constraints, true);
                        var strConstraint = string.IsNullOrEmpty(constraint) ? "" : $" {constraint}";

                        sb.AppendLine(
                            $"{name} {column.DataType}{identity}{require}{defaultValue}{strConstraint}{seperator}");
                    }

                    i++;
                }

                if (hasTableConstraints)
                {
                    sb.AppendLine(GetConstraints(table.Constraints));
                }

                sb.Append(")");
            }
            else
            {
                var builder = new SqliteStatementScriptBuilder();

                builder.BuildSelectStatement(selectStatement, false);

                sb.AppendLine(builder.ToString());
            }

            sb.AppendLine(";");

            return sb.ToString();
        }
    }
}