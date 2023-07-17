using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class StatementScriptBuilder : IDisposable
    {
        internal int Level;
        internal int LoopCount;
        internal string Indent => " ".PadLeft((Level + 1) * 2);
        public StringBuilder Script { get; } = new StringBuilder();

        public RoutineType RoutineType { get; set; }

        public List<DeclareVariableStatement> DeclareVariableStatements { get; internal set; } =
            new List<DeclareVariableStatement>();

        public List<DeclareCursorStatement> DeclareCursorStatements { get; internal set; } =
            new List<DeclareCursorStatement>();

        public List<Statement> OtherDeclareStatements { get; internal set; } = new List<Statement>();

        public List<Statement> SpecialStatements { get; internal set; } = new List<Statement>();
        public Dictionary<string, string> Replacements { get; internal set; } = new Dictionary<string, string>();
        public List<string> TemporaryTableNames { get; set; } = new List<string>();

        public StatementScriptBuilderOption Option { get; set; } = new StatementScriptBuilderOption();

        internal int Length => Script.Length;

        public void Dispose()
        {
            DeclareVariableStatements.Clear();
            DeclareCursorStatements.Clear();
            OtherDeclareStatements.Clear();
            SpecialStatements.Clear();
            Replacements.Clear();
            TemporaryTableNames.Clear();
        }

        public List<Statement> GetDeclareStatements()
        {
            var statements = new List<Statement>();

            statements.AddRange(DeclareVariableStatements);
            statements.AddRange(DeclareCursorStatements);
            statements.AddRange(OtherDeclareStatements);

            return statements;
        }

        protected void Append(string value, bool appendIndent = true)
        {
            Script.Append($"{(appendIndent ? Indent : "")}{value}");
        }

        protected void AppendLine(string value = "", bool appendIndent = true)
        {
            Append(value, appendIndent);
            Script.Append(Environment.NewLine);
        }

        protected virtual void PreHandleStatements(List<Statement> statements)
        {
        }

        protected void AppendChildStatements(IEnumerable<Statement> statements, bool needSeparator = true)
        {
            var statementList = new List<Statement>();

            statementList.AddRange(statements);

            PreHandleStatements(statementList);

            var childCount = statementList.Count();

            if (childCount > 0) IncreaseLevel();

            foreach (var statement in statementList) Build(statement, needSeparator);

            if (childCount > 0) DecreaseLevel();
        }

        public virtual StatementScriptBuilder Build(Statement statement, bool appendSeparator = true)
        {
            return this;
        }

        internal void IncreaseLevel()
        {
            Level++;
        }

        internal void DecreaseLevel()
        {
            Level--;
        }

        public void TrimEnd(params char[] characters)
        {
            if (characters != null && characters.Length > 0)
                while (Script.Length > 0 && characters.Contains(Script[Script.Length - 1]))
                    Script.Remove(Script.Length - 1, 1);
        }

        public void TrimSeparator()
        {
            TrimEnd(';', '\r', '\n', ' ');
        }

        public override string ToString()
        {
            return Script.ToString();
        }

        public void Clear()
        {
            Script.Clear();
        }

        protected virtual void BuildSelectStatement(SelectStatement select, bool appendSeparator = true)
        {
        }

        protected void BuildSelectStatementFromItems(SelectStatement selectStatement)
        {
            BuildFromItems(selectStatement.FromItems, selectStatement);
        }

        protected void BuildFromItems(List<FromItem> fromItems, SelectStatement selectStatement = null,
            bool isDeleteFromItem = false)
        {
            var count = fromItems.Count;
            var i = 0;

            var hasJoins = false;

            foreach (var fromItem in fromItems)
            {
                NameToken fromTableName = fromItem.TableName;

                if (fromTableName == null && selectStatement?.TableName != null)
                    fromTableName = selectStatement.TableName;

                var alias = fromItem.Alias;

                var isInvalidTableName = IsInvalidTableName(fromTableName?.Symbol);

                if (i == 0)
                    if (!isInvalidTableName)
                        Append("FROM ");

                hasJoins = fromItem.HasJoinItems;

                if (i > 0 && !hasJoins) Append(",", false);

                var nameWithAlias = GetNameWithAlias(fromTableName);

                if (!isInvalidTableName)
                    if (nameWithAlias?.Trim() != alias?.Symbol?.Trim())
                        Append($"{nameWithAlias}{(hasJoins ? Environment.NewLine : "")}", false);

                var hasSubSelect = false;

                if (fromItem.SubSelectStatement != null)
                {
                    hasSubSelect = true;

                    AppendLine("(");
                    BuildSelectStatement(fromItem.SubSelectStatement, false);
                    Append(")");

                    if (alias != null) Append($"{alias}", false);
                }

                if (fromItem.JoinItems.Count > 0)
                {
                    if (hasSubSelect) AppendLine();

                    var j = 0;

                    foreach (var joinItem in fromItem.JoinItems)
                    {
                        if (joinItem.Type == JoinType.PIVOT || joinItem.Type == JoinType.UNPIVOT)
                        {
                            if (joinItem.PivotItem != null)
                                BuildPivotItem(joinItem.PivotItem);
                            else if (joinItem.UnPivotItem != null) BuildUnPivotItem(joinItem.UnPivotItem);

                            if (joinItem.Alias != null)
                                AppendLine(joinItem.Alias.Symbol);
                            else
                                AppendLine(joinItem.Type + "_");
                        }
                        else
                        {
                            var isPostgresDeleteFromJoin = isDeleteFromItem && this is PostgreSqlStatementScriptBuilder;

                            var condition = joinItem.Condition == null
                                ? ""
                                : $" {(isPostgresDeleteFromJoin && j == 0 ? "WHERE" : "ON")} {joinItem.Condition}";

                            var joinKeyword = isPostgresDeleteFromJoin && j == 0 ? "USING " : $"{joinItem.Type} JOIN ";

                            var tableName = joinItem.TableName;

                            string joinTableName = null;

                            var isSubquery = AnalyserHelper.IsSubquery(tableName.Symbol);

                            if (!isSubquery)
                                joinTableName = GetNameWithAlias(tableName);
                            else
                                joinTableName =
                                    $"{StringHelper.GetParenthesisedString(tableName.Symbol)}{(tableName.Alias == null ? "" : $" {tableName.Alias}")}";

                            AppendLine($"{joinKeyword}{joinTableName}{condition}");
                        }

                        j++;
                    }
                }

                i++;
            }

            if (!hasJoins) AppendLine("", false);
        }

        protected void BuildIfCondition(IfStatementItem item)
        {
            if (item.Condition != null)
            {
                Append(item.Condition.Symbol);
            }
            else if (item.CondtionStatement != null)
            {
                if (item.ConditionType == IfConditionType.NotExists)
                    Append("NOT EXISTS");
                else if (item.ConditionType == IfConditionType.Exists) Append("EXISTS");

                AppendSubquery(item.CondtionStatement);
            }
        }

        protected void BuildUpdateSetValue(NameValueItem item)
        {
            if (item.Value != null)
                Append(item.Value.Symbol);
            else if (item.ValueStatement != null) AppendSubquery(item.ValueStatement);
        }

        public void AppendSubquery(SelectStatement statement)
        {
            Append("(");
            BuildSelectStatement(statement, false);
            Append(")");
        }

        protected virtual string GetNameWithAlias(NameToken name)
        {
            return name?.NameWithAlias;
        }

        public string GetTrimedQuotationValue(string value)
        {
            return value?.Trim('[', ']', '"', '`');
        }

        protected bool IsInvalidTableName(string tableName)
        {
            if (tableName == null) return false;

            tableName = GetTrimedQuotationValue(tableName);

            if (tableName.ToUpper() == "DUAL" && !(this is PlSqlStatementScriptBuilder)) return true;

            return false;
        }

        protected virtual string GetPivotInItem(TokenInfo token)
        {
            return GetTrimedQuotationValue(token.Symbol);
        }

        public void BuildPivotItem(PivotItem pivotItem)
        {
            AppendLine("PIVOT");
            AppendLine("(");
            AppendLine($"{pivotItem.AggregationFunctionName}({pivotItem.AggregatedColumnName})");
            AppendLine(
                $"FOR {pivotItem.ColumnName} IN ({string.Join(",", pivotItem.Values.Select(item => GetPivotInItem(item)))})");
            AppendLine(")");
        }

        public void BuildUnPivotItem(UnPivotItem unpivotItem)
        {
            AppendLine("UNPIVOT");
            AppendLine("(");
            AppendLine($"{unpivotItem.ValueColumnName}");
            AppendLine(
                $"FOR {unpivotItem.ForColumnName} IN ({string.Join(",", unpivotItem.InColumnNames.Select(item => $"{item}"))})");
            AppendLine(")");
        }

        protected bool HasAssignVariableColumn(SelectStatement statement)
        {
            var columns = statement.Columns;

            if (columns.Any(item => AnalyserHelper.IsAssignNameColumn(item))) return true;

            return false;
        }

        protected string GetNextLoopLabel(string prefix, bool withColon = true)
        {
            return $"{prefix}{++LoopCount}{(withColon ? ":" : "")}";
        }

        protected string GetCurrentLoopLabel(string prefix)
        {
            return $"{prefix}{LoopCount}";
        }

        protected string GetConstriants(List<ConstraintInfo> constaints, bool isForColumn = false)
        {
            if (constaints == null || constaints.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            var i = 0;

            foreach (var constraint in constaints)
            {
                var name = string.IsNullOrEmpty(constraint.Name?.Symbol) ? "" : $" {constraint.Name.Symbol}";

                var constraintType = constraint.Type;

                var definition = "";

                switch (constraintType)
                {
                    case ConstraintType.PrimaryKey:
                        definition = "PRIMARY KEY";

                        if (!isForColumn) definition += $" ({string.Join(",", constraint.ColumnNames)})";

                        break;
                    case ConstraintType.UniqueIndex:
                        definition = "UNIQUE";

                        if (!isForColumn) definition += $"({string.Join(",", constraint.ColumnNames)})";

                        break;
                    case ConstraintType.Check:
                        definition = $"CHECK {StringHelper.GetParenthesisedString(constraint?.Definition?.Symbol)}";
                        break;
                    case ConstraintType.ForeignKey:
                        var fki = constraint.ForeignKey;

                        if (fki != null)
                        {
                            if (!isForColumn) definition = $" FOREIGN KEY ({string.Join(",", fki.ColumnNames)})";

                            definition += $" REFERENCES {fki.RefTableName}({string.Join(",", fki.RefColumNames)})";

                            if (fki.UpdateCascade) definition += " UPDATE CASCADE";

                            if (fki.DeleteCascade) definition += " DELETE CASCADE";
                        }

                        break;
                }

                if (this is MySqlStatementScriptBuilder && isForColumn)
                {
                    sb.Append($" {definition}");
                }
                else
                {
                    var hasName = !string.IsNullOrEmpty(name);

                    if (hasName && isForColumn)
                        sb.Append($" {definition}");
                    else
                        sb.Append($"{(hasName ? "CONSTRAINT" : "")} {(!hasName ? "" : $"{name} ")}{definition}".Trim());
                }

                if (i < constaints.Count - 1) sb.AppendLine(",");

                i++;
            }

            return sb.ToString();
        }
    }
}