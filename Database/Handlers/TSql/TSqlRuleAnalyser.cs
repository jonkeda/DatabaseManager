using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using SqlAnalyser.Model;
using static TSqlParser;

namespace SqlAnalyser.Core
{
    public class TSqlRuleAnalyser : SqlRuleAnalyser
    {
        public TSqlRuleAnalyser(string content) : base(content)
        { }

        public override IEnumerable<Type> ParseTableTypes => new List<Type> { typeof(Full_table_nameContext) };

        public override IEnumerable<Type> ParseColumnTypes => new List<Type> { typeof(Full_column_nameContext) };
        public override IEnumerable<Type> ParseTableAliasTypes => new List<Type> { typeof(Table_aliasContext) };
        public override IEnumerable<Type> ParseColumnAliasTypes => new List<Type> { typeof(Column_aliasContext) };

        protected override Lexer GetLexer()
        {
            return new TSqlLexer(GetCharStreamFromString());
        }

        protected override Parser GetParser(CommonTokenStream tokenStream)
        {
            return new TSqlParser(tokenStream);
        }

        private Tsql_fileContext GetRootContext(out SqlSyntaxError error)
        {
            error = null;

            var parser = GetParser() as TSqlParser;

            var errorListener = AddParserErrorListener(parser);

            var context = parser.tsql_file();

            error = errorListener.Error;

            return context;
        }

        private Batch_level_statementContext GetBatchLevelStatementContext(out SqlSyntaxError error)
        {
            error = null;

            var rootContext = GetRootContext(out error);

            var batches = rootContext.batch();

            return rootContext.batch()?.FirstOrDefault()?.batch_level_statement();
        }

        public override SqlSyntaxError Validate()
        {
            SqlSyntaxError error = null;

            var rootContext = GetRootContext(out error);

            return error;
        }

        public override AnalyseResult AnalyseCommon()
        {
            SqlSyntaxError error = null;

            var rootContext = GetRootContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && rootContext != null)
            {
                CommonScript script = null;

                var batch = rootContext.batch().FirstOrDefault();

                if (batch != null)
                {
                    foreach (var child in batch.children)
                    {
                        if (child is Sql_clausesContext sc)
                        {
                            if (script == null)
                            {
                                script = new CommonScript();
                            }

                            script.Statements.AddRange(ParseSqlClause(sc));
                        }
                        else if (child is Batch_level_statementContext bls)
                        {
                            var proc = bls.create_or_alter_procedure();
                            var func = bls.create_or_alter_function();
                            var trigger = bls.create_or_alter_trigger()?.create_or_alter_dml_trigger();
                            var view = bls.create_view();

                            if (proc != null)
                            {
                                script = new RoutineScript { Type = RoutineType.PROCEDURE };

                                SetProcedureScript(script as RoutineScript, proc);
                            }
                            else if (func != null)
                            {
                                script = new RoutineScript { Type = RoutineType.FUNCTION };

                                SetFunctionScript(script as RoutineScript, func);
                            }
                            else if (trigger != null)
                            {
                                script = new TriggerScript();

                                SetTriggerScript(script as TriggerScript, trigger);
                            }
                            else if (view != null)
                            {
                                script = new ViewScript();

                                SetViewScript(script as ViewScript, view);
                            }
                        }
                    }

                    ExtractFunctions(script, batch);
                }

                result.Script = script;
            }

            return result;
        }

        public override AnalyseResult AnalyseProcedure()
        {
            SqlSyntaxError error = null;

            var batchLevelStatement = GetBatchLevelStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && batchLevelStatement != null)
            {
                var script = new RoutineScript { Type = RoutineType.PROCEDURE };

                var proc = batchLevelStatement.create_or_alter_procedure();

                if (proc != null)
                {
                    SetProcedureScript(script, proc);
                }

                ExtractFunctions(script, batchLevelStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetProcedureScript(RoutineScript script, Create_or_alter_procedureContext proc)
        {
            SetScriptName(script, proc.func_proc_name_schema().id_());

            SetRoutineParameters(script, proc.procedure_param());

            SetScriptBody(script, proc.sql_clauses());
        }

        private void SetScriptBody(CommonScript script, Sql_clausesContext[] clauses)
        {
            GotoStatement gotoStatement = null;

            foreach (var clause in clauses)
            {
                var gotoContext = clause.cfl_statement()?.goto_statement();

                if (gotoContext != null && !gotoContext.GetText().ToUpper().StartsWith("GOTO"))
                {
                    gotoStatement = ParseGotoStatement(gotoContext);

                    script.Statements.Add(gotoStatement);
                }
                else if (gotoStatement != null)
                {
                    gotoStatement.Statements.AddRange(ParseSqlClause(clause));
                }
                else
                {
                    script.Statements.AddRange(ParseSqlClause(clause));
                }
            }
        }

        public override AnalyseResult AnalyseFunction()
        {
            SqlSyntaxError error = null;

            var batchLevelStatement = GetBatchLevelStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && batchLevelStatement != null)
            {
                var script = new RoutineScript { Type = RoutineType.FUNCTION };

                var func = batchLevelStatement.create_or_alter_function();

                if (func != null)
                {
                    SetFunctionScript(script, func);
                }

                ExtractFunctions(script, batchLevelStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetFunctionScript(RoutineScript script, Create_or_alter_functionContext func)
        {
            SetScriptName(script, func.func_proc_name_schema().id_());

            SetRoutineParameters(script, func.procedure_param());

            SetFunctionDetails(script, func);
        }

        private void SetFunctionDetails(RoutineScript script, Create_or_alter_functionContext func)
        {
            var scalar = func.func_body_returns_scalar();
            var table = func.func_body_returns_table();
            var select = func.func_body_returns_select();

            if (scalar != null)
            {
                script.ReturnDataType = new TokenInfo(scalar.data_type().GetText()) { Type = TokenType.DataType };

                SetScriptBody(script, scalar.sql_clauses());

                #region ReturnStatement

                IParseTree t = null;
                for (var i = scalar.children.Count - 1; i >= 0; i--)
                {
                    if (scalar.children[i] is TerminalNodeImpl terminalNode)
                    {
                        if (terminalNode.Symbol.Type == RETURN)
                        {
                            if (t != null)
                            {
                                var returnStatement = new ReturnStatement
                                {
                                    Value = new TokenInfo(t as ParserRuleContext)
                                };

                                script.Statements.Add(returnStatement);

                                break;
                            }
                        }
                    }

                    t = scalar.children[i];
                }

                #endregion
            }
            else if (table != null)
            {
                script.ReturnTable = new TableInfo { IsTemporary = true, IsGlobal = false };

                foreach (var child in table.children)
                {
                    if (child is TerminalNodeImpl terminalNode)
                    {
                        if (terminalNode.Symbol.Text.StartsWith("@"))
                        {
                            script.ReturnTable.Name = new TokenInfo(terminalNode) { Type = TokenType.VariableName };
                        }
                    }
                    else if (child is Table_type_definitionContext type)
                    {
                        script.ReturnTable.Columns = type.column_def_table_constraints().column_def_table_constraint()
                            .Select(item =>
                                new ColumnInfo
                                {
                                    Name = ParseColumnName(item),
                                    DataType = new TokenInfo(item.column_definition().data_type())
                                        { Type = TokenType.DataType }
                                }).ToList();
                    }
                }

                script.Statements.AddRange(table.sql_clauses().SelectMany(item => ParseSqlClause(item)));
            }
            else if (select != null)
            {
                script.Statements.AddRange(
                    ParseSelectStatement(select.select_statement_standalone()?.select_statement()));
            }
        }

        public override AnalyseResult AnalyseView()
        {
            SqlSyntaxError error = null;

            var batchLevelStatement = GetBatchLevelStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && batchLevelStatement != null)
            {
                var script = new ViewScript();

                var view = batchLevelStatement.create_view();

                if (view != null)
                {
                    SetViewScript(script, view);
                }

                ExtractFunctions(script, batchLevelStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetViewScript(ViewScript script, Create_viewContext view)
        {
            #region Name

            SetScriptName(script, view.simple_name().id_());

            #endregion

            #region Statement

            foreach (var child in view.children)
            {
                if (child is Select_statementContext select)
                {
                    script.Statements.AddRange(ParseSelectStatement(select));
                }
                else if (child is Select_statement_standaloneContext standalone)
                {
                    script.Statements.AddRange(ParseSelectStandaloneContext(standalone));
                }
            }

            #endregion
        }

        public override AnalyseResult AnalyseTrigger()
        {
            SqlSyntaxError error = null;

            var batchLevelStatement = GetBatchLevelStatementContext(out error);

            var result = new AnalyseResult { Error = error };
            var script = new TriggerScript();

            if (!result.HasError && batchLevelStatement != null)
            {
                var trigger = batchLevelStatement.create_or_alter_trigger().create_or_alter_dml_trigger();

                if (trigger != null)
                {
                    SetTriggerScript(script, trigger);
                }

                ExtractFunctions(script, batchLevelStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetTriggerScript(TriggerScript script, Create_or_alter_dml_triggerContext trigger)
        {
            #region Name

            SetScriptName(script, trigger.simple_name().id_());

            #endregion

            script.TableName = new TableName(trigger.table_name());

            foreach (var child in trigger.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    switch (terminalNode.Symbol.Type)
                    {
                        case BEFORE:
                            script.Time = TriggerTime.BEFORE;
                            break;
                        case INSTEAD:
                            script.Time = TriggerTime.INSTEAD_OF;
                            break;
                        case AFTER:
                            script.Time = TriggerTime.AFTER;
                            break;
                    }
                }
                else if (child is Dml_trigger_operationContext operation)
                {
                    script.Events.Add((TriggerEvent)Enum.Parse(typeof(TriggerEvent), operation.GetText().ToUpper()));
                }
            }

            #region Body

            SetScriptBody(script, trigger.sql_clauses());

            #endregion
        }

        private List<Statement> ParseSqlClause(Sql_clausesContext node)
        {
            var statements = new List<Statement>();

            foreach (var bc in node.children)
            {
                if (bc is Another_statementContext another)
                {
                    statements.AddRange(ParseAnotherStatement(another));
                }
                else if (bc is Dml_clauseContext dml)
                {
                    statements.AddRange(ParseDmlStatement(dml));
                }
                else if (bc is Ddl_clauseContext ddl)
                {
                    statements.AddRange(ParseDdlStatement(ddl));
                }
                else if (bc is Cfl_statementContext cfl)
                {
                    statements.AddRange(ParseCflStatement(cfl));
                }
            }

            return statements;
        }

        private List<Statement> ParseDmlStatement(Dml_clauseContext node)
        {
            var statements = new List<Statement>();

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    if (child is Select_statement_standaloneContext selectStandalone)
                    {
                        statements.AddRange(ParseSelectStandaloneContext(selectStandalone));
                    }
                    else if (child is Select_statementContext select)
                    {
                        statements.AddRange(ParseSelectStatement(select));
                    }

                    if (child is Insert_statementContext insert)
                    {
                        statements.Add(ParseInsertStatement(insert));
                    }
                    else if (child is Update_statementContext update)
                    {
                        statements.Add(ParseUpdateStatement(update));
                    }
                    else if (child is Delete_statementContext delete)
                    {
                        statements.Add(ParseDeleteStatement(delete));
                    }
                }
            }

            return statements;
        }

        private List<Statement> ParseDdlStatement(Ddl_clauseContext node)
        {
            var statements = new List<Statement>();

            if (node.children != null)
            {
                Action<DatabaseObjectType, TokenType, ParserRuleContext[]> addDropStatement =
                    (objType, tokenType, objNames) =>
                    {
                        if (objNames != null)
                        {
                            foreach (var objName in objNames)
                            {
                                var dropStatement = new DropStatement
                                {
                                    ObjectType = objType,
                                    ObjectName = new NameToken(objName) { Type = tokenType }
                                };

                                if (objType == DatabaseObjectType.Table)
                                {
                                    dropStatement.IsTemporaryTable = IsTemporaryTable(objName);
                                }

                                statements.Add(dropStatement);
                            }
                        }
                    };

                foreach (var child in node.children)
                {
                    if (child is Create_tableContext createTable)
                    {
                        statements.Add(ParseCreateTableStatement(createTable));
                    }
                    else if (child is Truncate_tableContext truncate)
                    {
                        var truncateStatement = new TruncateStatement
                        {
                            TableName = ParseTableName(truncate.table_name())
                        };

                        statements.Add(truncateStatement);
                    }
                    else if (child is Drop_tableContext drop_Table)
                    {
                        addDropStatement(DatabaseObjectType.Table, TokenType.TableName, drop_Table.table_name());
                    }
                    else if (child is Drop_viewContext drop_View)
                    {
                        addDropStatement(DatabaseObjectType.View, TokenType.ViewName, drop_View.simple_name());
                    }
                    else if (child is Drop_functionContext drop_Function)
                    {
                        addDropStatement(DatabaseObjectType.Function, TokenType.FunctionName,
                            drop_Function.func_proc_name_schema());
                    }
                    else if (child is Drop_procedureContext drop_Procedure)
                    {
                        addDropStatement(DatabaseObjectType.Procedure, TokenType.ProcedureName,
                            drop_Procedure.func_proc_name_schema());
                    }
                    else if (child is Drop_triggerContext drop_Trigger)
                    {
                        addDropStatement(DatabaseObjectType.Trigger, TokenType.TriggerName,
                            drop_Trigger.drop_dml_trigger()?.simple_name());
                    }
                    else if (child is Drop_typeContext drop_Type)
                    {
                        addDropStatement(DatabaseObjectType.Type, TokenType.TypeName,
                            new ParserRuleContext[] { drop_Type.simple_name() });
                    }
                    else if (child is Drop_sequenceContext drop_Sequence)
                    {
                        addDropStatement(DatabaseObjectType.Sequence, TokenType.SequenceName,
                            new ParserRuleContext[] { drop_Sequence.sequence_name });
                    }
                }
            }

            return statements;
        }

        private InsertStatement ParseInsertStatement(Insert_statementContext node)
        {
            var statement = new InsertStatement();

            foreach (var child in node.children)
            {
                if (child is Ddl_objectContext table)
                {
                    statement.TableName = ParseTableName(table);
                }
                else if (child is Column_name_listContext columns)
                {
                    statement.Columns = columns.id_().Select(item => ParseColumnName(item)).ToList();
                }
                else if (child is Insert_column_name_listContext insertColumns)
                {
                    statement.Columns = insertColumns.insert_column_id().Select(item => ParseColumnName(item)).ToList();
                }
                else if (child is Insert_statement_valueContext values)
                {
                    var tableValues = values.table_value_constructor();
                    var derivedTable = values.derived_table();

                    if (tableValues != null)
                    {
                        statement.Values = tableValues.expression_list().SelectMany(item =>
                            item.expression().Select(t => new TokenInfo(t) { Type = TokenType.InsertValue })).ToList();
                    }
                    else if (derivedTable != null)
                    {
                        statement.SelectStatements = new List<SelectStatement>();

                        statement.SelectStatements.AddRange(
                            ParseSelectStatement(derivedTable.subquery().FirstOrDefault()?.select_statement()));
                    }
                }
            }

            return statement;
        }

        private UpdateStatement ParseUpdateStatement(Update_statementContext node)
        {
            var statement = new UpdateStatement();

            var name = node.ddl_object();

            statement.TableNames.Add(ParseTableName(name));

            foreach (var ele in node.update_elem())
            {
                var valExp = ele.expression();
                var subquery = valExp.bracket_expression()?.subquery();

                var setItem = new NameValueItem();

                if (subquery == null)
                {
                    setItem.Value = CreateToken(valExp, TokenType.UpdateSetValue);
                }
                else
                {
                    setItem.ValueStatement = ParseSelectStatement(subquery.select_statement())?.FirstOrDefault();
                }

                var columnName = ele.full_column_name();

                if (columnName != null)
                {
                    setItem.Name = ParseColumnName(columnName);
                }
                else
                {
                    foreach (var child in ele.children)
                    {
                        if (child is TerminalNodeImpl impl && child.GetText().StartsWith("@"))
                        {
                            setItem.Name = new TokenInfo(impl) { Type = TokenType.VariableName };
                            break;
                        }
                    }
                }

                statement.SetItems.Add(setItem);

                var value = setItem.Value;

                if (value != null && value.Symbol?.StartsWith("@") == false)
                {
                    AddChildTableAndColumnNameToken(valExp, value);
                }
            }

            var fromTable = node.table_sources();

            if (fromTable != null)
            {
                statement.FromItems = ParseTableScources(fromTable);
            }

            statement.Condition = ParseCondition(node.search_condition());

            return statement;
        }

        private List<FromItem> ParseTableScources(Table_sourcesContext node)
        {
            var fromItems = new List<FromItem>();

            foreach (var child in node.children)
            {
                if (child is Table_sourceContext ts)
                {
                    var fromItem = new FromItem();

                    var tsi = ts.table_source_item_joined();

                    var fromTable = tsi.table_source_item();

                    var joinParts = new List<Join_partContext>();

                    var asWhole = false;

                    if (fromTable != null)
                    {
                        var alias = fromTable.as_table_alias();

                        if (alias != null)
                        {
                            fromItem.Alias = new TokenInfo(alias.table_alias());
                        }

                        var derivedTable = fromTable.derived_table();

                        if (derivedTable != null)
                        {
                            fromItem.SubSelectStatement = ParseDerivedTable(derivedTable);
                        }
                        else
                        {
                            fromItem.TableName = ParseTableName(fromTable);
                        }
                    }
                    else
                    {
                        asWhole = true;

                        var joined = tsi.table_source_item_joined();

                        fromItem.TableName = new TableName(joined);

                        AddChildTableAndColumnNameToken(joined, fromItem.TableName);
                    }

                    var joins = tsi.join_part();

                    if (joins != null)
                    {
                        joinParts.AddRange(joins);
                    }

                    foreach (var join in joinParts)
                    {
                        var tsij = join.join_on()?.table_source()?.table_source_item_joined();

                        if (!asWhole && (tsij.table_source_item_joined() != null ||
                                         tsij.table_source_item()?.derived_table() != null))
                        {
                            asWhole = true;
                        }

                        var joinItems = ParseJoin(join, asWhole);

                        if (joinItems.Count > 1)
                        {
                            for (var i = joinItems.Count - 1; i > 0; i--)
                            {
                                var currentJoinItem = joinItems[i];

                                if (i - 1 > 0)
                                {
                                    var previousJoinItem = joinItems[i - 1];

                                    var previousJoinTableName = new TableName(previousJoinItem.TableName.Symbol);
                                    ObjectHelper.CopyProperties(previousJoinItem.TableName, previousJoinTableName);

                                    var currentJoinTableName = new TableName(currentJoinItem.TableName.Symbol);
                                    ObjectHelper.CopyProperties(currentJoinItem.TableName, currentJoinTableName);

                                    joinItems[i - 1].TableName = currentJoinTableName;
                                    joinItems[i].TableName = previousJoinTableName;
                                }
                            }
                        }

                        fromItem.JoinItems.AddRange(joinItems);
                    }

                    fromItems.Add(fromItem);
                }
            }

            return fromItems;
        }

        private List<JoinItem> ParseJoin(Join_partContext node, bool asWhole = false)
        {
            var joinItems = new List<JoinItem>();

            var joinItem = new JoinItem();

            var joinOn = node.join_on();

            if (joinOn != null)
            {
                foreach (var child in joinOn.children)
                {
                    if (child is TerminalNodeImpl terminalNode)
                    {
                        var type = terminalNode.Symbol.Type;

                        switch (type)
                        {
                            case INNER:
                                joinItem.Type = JoinType.INNER;
                                break;
                            case LEFT:
                                joinItem.Type = JoinType.LEFT;
                                break;
                            case RIGHT:
                                joinItem.Type = JoinType.RIGHT;
                                break;
                            case FULL:
                                joinItem.Type = JoinType.FULL;
                                break;
                            case CROSS:
                                joinItem.Type = JoinType.CROSS;
                                break;
                            case PIVOT:
                                joinItem.Type = JoinType.PIVOT;
                                break;
                            case UNPIVOT:
                                joinItem.Type = JoinType.UNPIVOT;
                                break;
                        }
                    }
                }
            }

            var tableSoure = joinOn?.table_source();
            var pivot = node.pivot()?.pivot_clause();
            var unpivot = node.unpivot()?.unpivot_clause();

            var alias = tableSoure?.table_source_item_joined()?.table_source_item()?.as_table_alias();

            if (alias != null)
            {
                joinItem.Alias = new TokenInfo(alias.table_alias());
            }

            joinItems.Add(joinItem);

            if (tableSoure != null)
            {
                joinItem.TableName = asWhole ? new TableName(tableSoure) : ParseTableName(tableSoure);
                joinItem.Condition = ParseCondition(joinOn.search_condition());

                if (!asWhole)
                {
                    var join = tableSoure.table_source_item_joined();

                    if (join != null)
                    {
                        var joinParts = join.join_part();

                        var childJoinItems = joinParts.SelectMany(item => ParseJoin(item)).ToList();

                        joinItems.AddRange(childJoinItems);
                    }
                }
                else
                {
                    #region handle alias

                    var ts = tableSoure.table_source_item_joined()?.table_source_item();
                    var derivedTable = ts?.derived_table();
                    var derivedTableAlias = ts?.as_table_alias()?.table_alias();

                    if (derivedTable != null && derivedTableAlias != null)
                    {
                        joinItem.TableName = new TableName(derivedTable)
                        {
                            Alias = new TokenInfo(derivedTableAlias)
                        };

                        foreach (var child in ts.children)
                        {
                            if (child is TerminalNodeImpl tni)
                            {
                                var text = child.GetText().Trim();

                                if (text == "(" && tni.Symbol.StartIndex < joinItem.TableName.StartIndex)
                                {
                                    joinItem.TableName.StartIndex = tni.Symbol.StartIndex;
                                }
                                else if (text == ")" && tni.Symbol.StopIndex > joinItem.TableName.StopIndex)
                                {
                                    joinItem.TableName.StopIndex = tni.Symbol.StopIndex;
                                    break;
                                }
                            }
                        }
                    }

                    #endregion

                    AddChildTableAndColumnNameToken(tableSoure, joinItem.TableName);

                    AddNodeVariablesChildren(tableSoure, joinItem.TableName);
                }
            }
            else if (pivot != null)
            {
                joinItem.PivotItem = ParsePivot(pivot);
            }
            else if (unpivot != null)
            {
                joinItem.UnPivotItem = ParseUnPivot(unpivot);
            }

            return joinItems;
        }

        private PivotItem ParsePivot(Pivot_clauseContext node)
        {
            var pivotItem = new PivotItem();

            var function = node.aggregate_windowed_function();

            pivotItem.AggregationFunctionName = new TokenInfo(function.children[0] as TerminalNodeImpl);
            pivotItem.AggregatedColumnName = ParseColumnName(function.all_distinct_expression()?.expression());
            pivotItem.ColumnName = ParseColumnName(node.full_column_name());
            pivotItem.Values = node.column_alias_list().column_alias().Select(item => new TokenInfo(item)).ToList();

            return pivotItem;
        }

        private UnPivotItem ParseUnPivot(Unpivot_clauseContext node)
        {
            var unpivotItem = new UnPivotItem
            {
                ValueColumnName = ParseColumnName(node.expression().full_column_name()),
                ForColumnName = ParseColumnName(node.full_column_name()),
                InColumnNames = node.full_column_name_list().full_column_name()
                    .Select(item => ParseColumnName(item)).ToList()
            };

            return unpivotItem;
        }

        private SelectStatement ParseDerivedTable(Derived_tableContext node)
        {
            var statement = new SelectStatement();

            foreach (var child in node.children)
            {
                if (child is SubqueryContext subquery)
                {
                    statement = ParseSelectStatement(subquery.select_statement()).FirstOrDefault();
                }
            }

            return statement;
        }

        private DeleteStatement ParseDeleteStatement(Delete_statementContext node)
        {
            var statement = new DeleteStatement
            {
                TableName = ParseTableName(node.delete_statement_from().ddl_object())
            };

            var fromTable = node.table_sources();

            if (fromTable != null)
            {
                statement.FromItems = ParseTableScources(fromTable);

                if (!AnalyserHelper.IsFromItemsHaveJoin(statement.FromItems) && statement.FromItems.Count > 0)
                {
                    statement.TableName = statement.FromItems[0].TableName;
                }
            }

            statement.Condition = ParseCondition(node.search_condition());

            return statement;
        }

        private List<Statement> ParseCflStatement(Cfl_statementContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is If_statementContext @if)
                {
                    statements.Add(ParseIfStatement(@if));
                }

                if (child is While_statementContext @while)
                {
                    statements.Add(ParseWhileStatement(@while));
                }
                else if (child is Block_statementContext block)
                {
                    foreach (var bc in block.children)
                    {
                        if (bc is Sql_clausesContext clauses)
                        {
                            statements.AddRange(ParseSqlClause(clauses));
                        }
                        else if (child is Return_statementContext @return)
                        {
                            statements.Add(ParseReturnStatement(@return));
                        }
                        else if (child is Break_statementContext @break)
                        {
                            statements.Add(new BreakStatement());
                        }
                        else if (child is Continue_statementContext @continue)
                        {
                            statements.Add(new ContinueStatement());
                        }
                        else if (child is Try_catch_statementContext trycatch)
                        {
                            statements.Add(ParseTryCatchStatement(trycatch));
                        }
                        else if (child is Print_statementContext print)
                        {
                            statements.Add(ParsePrintStatement(print));
                        }
                        else if (child is Raiseerror_statementContext raiseError)
                        {
                            statements.Add(ParseRaiseErrorStatement(raiseError));
                        }
                        else if (child is Goto_statementContext gs)
                        {
                            statements.Add(ParseGotoStatement(gs));
                        }
                    }
                }
            }

            return statements;
        }

        private IfStatement ParseIfStatement(If_statementContext node, bool isElseIf = false)
        {
            var statement = new IfStatement();

            var ifItem = new IfStatementItem { Type = isElseIf ? IfStatementType.ELSEIF : IfStatementType.IF };

            var condition = node.search_condition();

            var hasNot = false;

            foreach (var child in condition.children)
            {
                if (child is TerminalNodeImpl && child.GetText().ToUpper() == "NOT")
                {
                    hasNot = true;
                }
                else if (child is PredicateContext predicate)
                {
                    foreach (var c in predicate.children)
                    {
                        if (c is TerminalNodeImpl && c.GetText().ToUpper() == "EXISTS")
                        {
                            ifItem.ConditionType = IfConditionType.Exists;
                        }
                        else if (c is SubqueryContext subquery)
                        {
                            ifItem.CondtionStatement =
                                ParseSelectStatement(subquery.select_statement())?.FirstOrDefault();
                        }
                    }
                }
            }

            if (ifItem.ConditionType == IfConditionType.Exists && hasNot)
            {
                ifItem.ConditionType = IfConditionType.NotExists;
            }

            if (ifItem.CondtionStatement == null)
            {
                ifItem.Condition = ParseCondition(condition);
            }

            var sqlClauses = node.sql_clauses();

            if (sqlClauses != null && sqlClauses.Length > 0)
            {
                var statementClause = sqlClauses[0];

                ifItem.Statements.AddRange(ParseSqlClause(statementClause));

                statement.Items.Add(ifItem);

                if (sqlClauses.Length > 1)
                {
                    var cfl = sqlClauses[1].cfl_statement();
                    var ifClause = cfl?.if_statement();

                    var block = cfl?.block_statement();

                    if (ifClause != null)
                    {
                        var elseIfStatement = ParseIfStatement(ifClause, true);

                        statement.Items.AddRange(elseIfStatement.Items);
                    }
                    else if (block != null)
                    {
                        var elseItem = new IfStatementItem { Type = IfStatementType.ELSE };

                        elseItem.Statements.AddRange(block.sql_clauses().SelectMany(item => ParseSqlClause(item)));

                        statement.Items.Add(elseItem);
                    }
                }
            }

            return statement;
        }

        public WhileStatement ParseWhileStatement(While_statementContext node)
        {
            var statement = new WhileStatement();

            foreach (var child in node.children)
            {
                if (child is Search_conditionContext condition)
                {
                    statement.Condition = ParseCondition(condition);
                }
                else if (child is Sql_clausesContext clause)
                {
                    statement.Statements.AddRange(ParseSqlClause(clause));
                }
            }

            return statement;
        }

        private List<Statement> ParseAnotherStatement(Another_statementContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Declare_statementContext declare)
                {
                    statements.AddRange(ParseDeclareStatement(declare));
                }
                else if (child is Set_statementContext set)
                {
                    statements.Add(ParseSetStatement(set));
                }
                else if (child is Execute_statementContext execute)
                {
                    statements.Add(ParseExecuteStatement(execute));
                }
                else if (child is Transaction_statementContext transaction)
                {
                    statements.Add(ParseTransactionStatment(transaction));
                }
                else if (child is Cursor_statementContext cursor)
                {
                    statements.Add(ParseCursorStatement(cursor));
                }
            }

            return statements;
        }

        private List<Statement> ParseDeclareStatement(Declare_statementContext node)
        {
            var statements = new List<Statement>();

            foreach (var dc in node.children)
            {
                if (dc is Declare_localContext local)
                {
                    var declareStatement = new DeclareVariableStatement
                    {
                        Name = new TokenInfo(local.LOCAL_ID()) { Type = TokenType.VariableName },
                        DataType = new TokenInfo(local.data_type())
                    };

                    var expression = local.expression();

                    if (expression != null)
                    {
                        declareStatement.DefaultValue = new TokenInfo(expression);
                    }

                    statements.Add(declareStatement);
                }
                else if (dc is Declare_cursorContext cursor)
                {
                    statements.Add(ParseDeclareCursor(cursor));
                }
                else if (dc is Table_type_definitionContext table)
                {
                    var declareStatement = new DeclareTableStatement();

                    var tableInfo = new TableInfo
                    {
                        IsTemporary = true, IsGlobal = false,
                        Name = new TokenInfo(node.LOCAL_ID()) { Type = TokenType.VariableName }
                    };

                    var columns = table.column_def_table_constraints().column_def_table_constraint();

                    tableInfo.Columns = columns.Select(item => new ColumnInfo
                    {
                        Name = ParseColumnName(item.column_definition().id_()),
                        DataType = new TokenInfo(item.column_definition().data_type())
                    }).ToList();

                    declareStatement.TableInfo = tableInfo;

                    statements.Add(declareStatement);
                }
                else if (dc is TerminalNodeImpl impl)
                {
                    if (impl.GetText().StartsWith("@") && node.table_type_definition() == null)
                    {
                        var declareStatement = new DeclareVariableStatement
                        {
                            Name = new TokenInfo(impl) { Type = TokenType.VariableName },
                            DataType = new TokenInfo(node.table_name())
                        };

                        statements.Add(declareStatement);
                    }
                }
            }

            return statements;
        }

        private void SetScriptName(CommonScript script, Id_Context[] ids)
        {
            var name = ids.Last();

            script.Name = new TokenInfo(name);

            if (ids.Length > 1)
            {
                script.Schema = ids.First().GetText();
            }
        }

        private void SetParameterType(Parameter parameterInfo, IList<IParseTree> nodes)
        {
            foreach (var child in nodes)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    if (terminalNode.Symbol.Type == OUT || terminalNode.Symbol.Type == OUTPUT)
                    {
                        parameterInfo.ParameterType = ParameterType.OUT;
                    }
                }
            }
        }

        private void SetRoutineParameters(RoutineScript script, Procedure_paramContext[] parameters)
        {
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    var parameterInfo = new Parameter
                    {
                        Name = new TokenInfo(parameter.children[0] as TerminalNodeImpl)
                            { Type = TokenType.ParameterName },
                        DataType = new TokenInfo(parameter.data_type().GetText())
                            { Type = TokenType.DataType }
                    };

                    var defaultValue = parameter.default_value();

                    if (defaultValue != null)
                    {
                        parameterInfo.DefaultValue = new TokenInfo(defaultValue);
                    }

                    SetParameterType(parameterInfo, parameter.children);

                    script.Parameters.Add(parameterInfo);
                }
            }
        }

        private List<SelectStatement> ParseSelectStandaloneContext(Select_statement_standaloneContext node)
        {
            var statements = new List<SelectStatement>();

            List<WithStatement> withStatements = null;

            foreach (var child in node.children)
            {
                if (child is Select_statementContext select)
                {
                    statements.AddRange(ParseSelectStatement(select));
                }
                else if (child is With_expressionContext with)
                {
                    withStatements = ParseWithStatement(with);
                }
            }

            if (statements.Count > 0)
            {
                statements.First().WithStatements = withStatements;
            }

            return statements;
        }

        private List<SelectStatement> ParseSelectStatement(Select_statementContext node)
        {
            var statements = new List<SelectStatement>();

            SelectStatement selectStatement = null;

            var orderbyList = new List<TokenInfo>();
            TokenInfo option = null;
            SelectLimitInfo selectLimitInfo = null;

            foreach (var child in node.children)
            {
                if (child is Query_expressionContext query)
                {
                    foreach (var qc in query.children)
                    {
                        if (qc is Query_specificationContext specification)
                        {
                            selectStatement = ParseQuerySpecification(specification);
                        }
                        else if (qc is Sql_unionContext union)
                        {
                            if (selectStatement.UnionStatements == null)
                            {
                                selectStatement.UnionStatements = new List<UnionStatement>();
                            }

                            selectStatement.UnionStatements.Add(ParseUnionSatement(union));
                        }
                        else if (qc is Query_expressionContext exp)
                        {
                            var querySpec = exp.query_specification();

                            if (querySpec != null)
                            {
                                selectStatement = ParseQuerySpecification(querySpec);
                            }
                        }
                    }

                    if (selectStatement != null)
                    {
                        statements.Add(selectStatement);
                    }
                }
                else if (child is Select_order_by_clauseContext order)
                {
                    var isLimit = false;
                    var limitKeyword = 0;

                    foreach (var oc in order.children)
                    {
                        if (oc is Order_by_clauseContext orderByClause)
                        {
                            var expressions = orderByClause.order_by_expression();

                            if (expressions != null)
                            {
                                orderbyList.AddRange(expressions.Select(item => CreateToken(item, TokenType.OrderBy)));
                            }
                        }
                        else if (oc is Order_by_expressionContext orderByExp)
                        {
                            orderbyList.Add(CreateToken(orderByExp, TokenType.OrderBy));
                        }
                        else if (oc is TerminalNodeImpl terminalNode)
                        {
                            if ((limitKeyword = terminalNode.Symbol.Type) == OFFSET)
                            {
                                isLimit = true;
                            }
                        }
                        else if (oc is ExpressionContext exp)
                        {
                            if (isLimit)
                            {
                                if (selectLimitInfo == null)
                                {
                                    selectLimitInfo = new SelectLimitInfo();
                                }

                                if (limitKeyword == OFFSET)
                                {
                                    selectLimitInfo.StartRowIndex = new TokenInfo(exp);
                                }
                                else if (limitKeyword == NEXT)
                                {
                                    selectLimitInfo.RowCount = new TokenInfo(exp);
                                }
                            }
                        }
                    }
                }
                else if (child is Option_clauseContext opt)
                {
                    option = new TokenInfo(opt) { Type = TokenType.Option };
                }

                if (selectStatement != null)
                {
                    if (orderbyList.Count > 0)
                    {
                        selectStatement.OrderBy = orderbyList;
                    }

                    if (selectLimitInfo != null)
                    {
                        selectStatement.LimitInfo = selectLimitInfo;
                    }

                    selectStatement.Option = option;
                }
            }

            return statements;
        }

        private List<WithStatement> ParseWithStatement(With_expressionContext node)
        {
            var statements = new List<WithStatement>();

            var tables = node.common_table_expression();

            if (tables != null)
            {
                foreach (var table in tables)
                {
                    var statement = new WithStatement
                    {
                        Name = new TableName(table.id_()) { Type = TokenType.General }
                    };

                    var cols = table.column_name_list();

                    if (cols != null)
                    {
                        statement.Columns = cols.id_().Select(item => ParseColumnName(item)).ToList();
                    }

                    statement.SelectStatements = ParseSelectStatement(table.select_statement());

                    statements.Add(statement);
                }
            }

            return statements;
        }

        private SelectStatement ParseQuerySpecification(Query_specificationContext node)
        {
            var statement = new SelectStatement();

            var terminalNodeType = 0;

            foreach (var child in node.children)
            {
                if (child is Select_listContext list)
                {
                    statement.Columns.AddRange(list.select_list_elem().Select(item => ParseColumnName(item)));
                }
                else if (child is TerminalNodeImpl terminalNode)
                {
                    terminalNodeType = terminalNode.Symbol.Type;

                    if (terminalNodeType == INTO)
                    {
                        statement.Intos = new List<TokenInfo> { new TableName(node.table_name()) };
                    }
                }
                else if (child is Table_sourcesContext table)
                {
                    if (!AnalyserHelper.IsSubQuery(table))
                    {
                        //statement.TableName = this.ParseTableName(table);
                    }

                    statement.FromItems = ParseTableScources(table);
                }
                else if (child is Search_conditionContext condition)
                {
                    switch (terminalNodeType)
                    {
                        case WHERE:
                            statement.Where = ParseCondition(condition);
                            break;
                        case HAVING:
                            statement.Having = ParseCondition(condition);
                            break;
                    }
                }
                else if (child is Group_by_itemContext groupBy)
                {
                    if (statement.GroupBy == null)
                    {
                        statement.GroupBy = new List<TokenInfo>();
                    }

                    var gpb = CreateToken(groupBy, TokenType.GroupBy);

                    statement.GroupBy.Add(gpb);

                    if (!AnalyserHelper.IsValidColumnName(gpb))
                    {
                        AddChildTableAndColumnNameToken(groupBy, gpb);
                    }
                }
                else if (child is Top_clauseContext top)
                {
                    var topCount = top.top_count();

                    statement.TopInfo = new SelectTopInfo
                    {
                        TopCount = new TokenInfo(topCount)
                    };

                    var text = topCount.GetText();

                    if (text.Contains("@"))
                    {
                        if (text.StartsWith("(") && topCount.expression() != null)
                        {
                            statement.TopInfo.TopCount = new TokenInfo(topCount.expression());
                        }

                        statement.TopInfo.TopCount.Type = TokenType.VariableName;
                    }

                    statement.TopInfo.IsPercent = node.select_list().select_list_elem()
                        .Any(item => item.children.Any(t => t?.GetText()?.ToUpper() == "PERCENT"));
                }
            }

            return statement;
        }

        private UnionStatement ParseUnionSatement(Sql_unionContext node)
        {
            var statement = new UnionStatement();

            var unionType = UnionType.UNION;

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    var type = terminalNode.Symbol.Type;

                    switch (type)
                    {
                        case ALL:
                            unionType = UnionType.UNION_ALL;
                            break;
                        case INTERSECT:
                            unionType = UnionType.INTERSECT;
                            break;
                        case EXCEPT:
                            unionType = UnionType.EXCEPT;
                            break;
                    }
                }
                else if (child is Query_specificationContext spec)
                {
                    statement.Type = unionType;
                    statement.SelectStatement = ParseQuerySpecification(spec);
                }
            }

            return statement;
        }

        private SetStatement ParseSetStatement(Set_statementContext node)
        {
            var statement = new SetStatement();

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    var type = terminalNode.Symbol.Type;
                    var text = terminalNode.GetText();

                    if (type != SET && text != "=" && statement.Key == null)
                    {
                        statement.Key = new TokenInfo(terminalNode);

                        if (text.StartsWith("@"))
                        {
                            statement.Key.Type = TokenType.VariableName;
                        }
                    }
                }
                else if (child is ExpressionContext expression)
                {
                    statement.Value = CreateToken(expression);

                    var subquery = expression.bracket_expression()?.subquery();
                    var functionCall = expression.function_call();

                    if (subquery != null)
                    {
                        statement.Value.Type = TokenType.Subquery;

                        AddChildTableAndColumnNameToken(expression, statement.Value);
                    }
                    else if (functionCall != null)
                    {
                        statement.Value.Type = TokenType.FunctionCall;

                        foreach (var c in functionCall.children)
                        {
                            if (c is Scalar_function_nameContext sfn)
                            {
                                statement.Value.AddChild(new NameToken(sfn) { Type = TokenType.FunctionName });
                                break;
                            }
                        }
                    }
                    else
                    {
                        var exps = expression.expression();

                        if (exps != null)
                        {
                            foreach (var exp in exps)
                            {
                                if (exp.GetText().StartsWith("@"))
                                {
                                    statement.Value.AddChild(new TokenInfo(exp) { Type = TokenType.VariableName });
                                }
                            }
                        }
                    }

                    break;
                }
                else if (child is Declare_set_cursor_commonContext dscm)
                {
                    statement.IsSetCursorVariable = true;
                    statement.ValueStatement = ParseSelectStandaloneContext(dscm.select_statement_standalone())
                        ?.FirstOrDefault();
                }
            }

            return statement;
        }

        public Statement ParseReturnStatement(Return_statementContext node)
        {
            Statement statement = null;

            var expressioin = node.expression();

            if (expressioin != null)
            {
                statement = new ReturnStatement { Value = new TokenInfo(expressioin) };
            }
            else
            {
                statement = new LeaveStatement { Content = new TokenInfo(node) };
            }

            return statement;
        }

        private TryCatchStatement ParseTryCatchStatement(Try_catch_statementContext node)
        {
            var statement = new TryCatchStatement();

            var sqlClauses = node.sql_clauses();

            foreach (var sc in sqlClauses)
            {
                statement.TryStatements.AddRange(ParseSqlClause(sc));
            }

            statement.CatchStatements.AddRange(ParseSqlClause(node.try_clauses));

            return statement;
        }

        private PrintStatement ParsePrintStatement(Print_statementContext node)
        {
            var statement = new PrintStatement
            {
                Content = new TokenInfo(node.expression())
            };

            return statement;
        }

        private CallStatement ParseExecuteStatement(Execute_statementContext node)
        {
            var statement = new CallStatement();

            var body = node.execute_body();

            statement.Name = new TokenInfo(body.func_proc_name_server_database_schema())
                { Type = TokenType.RoutineName };

            var args = body.execute_statement_arg();
            var varStrs = body.execute_var_string();

            ParseExecuteStatementArgument(statement.Parameters, args);

            if (varStrs != null && varStrs.Length > 0)
            {
                statement.IsExecuteSql = true;

                statement.Parameters.AddRange(varStrs.Select(item => new CallParameter
                    { Value = new TokenInfo(item) }));
            }

            if (statement.Name.Symbol == "SP_EXECUTESQL")
            {
                statement.IsExecuteSql = true;

                if (statement.Parameters.Count > 1)
                {
                    statement.Parameters[1].IsDescription = true;
                }
            }

            return statement;
        }

        private void ParseExecuteStatementArgument(List<CallParameter> parameters, Execute_statement_argContext node)
        {
            if (node != null)
            {
                var namedArgs = node.execute_statement_arg_named();
                var unnamedArg = node.execute_statement_arg_unnamed();
                var execArgs = node.execute_statement_arg();

                if (namedArgs != null && namedArgs.Length > 0)
                {
                    foreach (var g in namedArgs)
                    {
                        var parameter = ParseCallParameter(g.execute_parameter());

                        if (parameter != null)
                        {
                            foreach (var child in g.children)
                            {
                                if (child is TerminalNodeImpl impl && child.GetText().StartsWith("@"))
                                {
                                    parameter.Name = new TokenInfo(impl)
                                        { Type = TokenType.VariableName };
                                    break;
                                }
                            }

                            parameters.Add(parameter);
                        }
                    }
                }

                if (unnamedArg != null)
                {
                    parameters.Add(ParseCallParameter(unnamedArg.execute_parameter()));
                }

                if (execArgs != null)
                {
                    foreach (var item in execArgs)
                    {
                        ParseExecuteStatementArgument(parameters, item);
                    }
                }
            }
        }

        private CallParameter ParseCallParameter(Execute_parameterContext node)
        {
            var parameter = new CallParameter();

            if (node != null)
            {
                foreach (var child in node.children)
                {
                    var text = child.GetText().ToUpper();

                    if (child is TerminalNodeImpl tni)
                    {
                        if (text.StartsWith("@"))
                        {
                            parameter.Value = new TokenInfo(tni) { Type = TokenType.VariableName };
                        }
                        else if (text == "OUTPUT")
                        {
                            parameter.ParameterType = ParameterType.OUT;
                        }
                        else if (text == "NULL")
                        {
                            parameter.Value = new TokenInfo(text);
                        }
                    }
                    else if (child is ConstantContext cc)
                    {
                        parameter.Value = new TokenInfo(cc);
                    }
                }
            }

            return parameter;
        }

        private TransactionStatement ParseTransactionStatment(Transaction_statementContext node)
        {
            var statement = new TransactionStatement();

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    var type = terminalNode.Symbol.Type;

                    if (type == BEGIN)
                    {
                        statement.CommandType = TransactionCommandType.BEGIN;
                    }
                    else if (type == COMMIT)
                    {
                        statement.CommandType = TransactionCommandType.COMMIT;
                    }
                    else if (type == ROLLBACK)
                    {
                        statement.CommandType = TransactionCommandType.ROLLBACK;
                    }
                }
            }

            var content = node.id_();

            if (content != null)
            {
                statement.Content = new TokenInfo(content);
            }

            return statement;
        }

        private Statement ParseCursorStatement(Cursor_statementContext node)
        {
            Statement statement = null;

            var isOpen = false;
            var isClose = false;
            var isDeallocate = false;

            foreach (var child in node.children)
            {
                if (child is Declare_cursorContext declare)
                {
                    statement = ParseDeclareCursor(declare);
                }
                else if (child is TerminalNodeImpl terminalNode)
                {
                    var type = terminalNode.Symbol.Type;

                    if (type == OPEN)
                    {
                        isOpen = true;
                    }
                    else if (type == CLOSE)
                    {
                        isClose = true;
                    }
                    else if (type == DEALLOCATE)
                    {
                        isDeallocate = true;
                    }
                }
                else if (child is Cursor_nameContext name)
                {
                    if (isOpen)
                    {
                        var openCursorStatement = new OpenCursorStatement
                        {
                            CursorName = new TokenInfo(name) { Type = TokenType.CursorName }
                        };

                        statement = openCursorStatement;
                    }
                    else if (isClose)
                    {
                        var closeCursorStatement = new CloseCursorStatement
                        {
                            CursorName = new TokenInfo(name) { Type = TokenType.CursorName }
                        };

                        statement = closeCursorStatement;
                    }
                    else if (isDeallocate)
                    {
                        var deallocateCursorStatement = new DeallocateCursorStatement
                        {
                            CursorName = new TokenInfo(name) { Type = TokenType.CursorName }
                        };

                        statement = deallocateCursorStatement;
                    }
                }
                else if (child is Fetch_cursorContext fetch)
                {
                    var fetchCursorStatement = new FetchCursorStatement
                    {
                        CursorName = new TokenInfo(fetch.cursor_name())
                            { Type = TokenType.CursorName }
                    };

                    foreach (var fc in fetch.children)
                    {
                        if (fc is TerminalNodeImpl tn)
                        {
                            var text = tn.GetText();

                            if (text.StartsWith("@"))
                            {
                                fetchCursorStatement.Variables.Add(new TokenInfo(tn) { Type = TokenType.VariableName });
                            }
                        }
                    }

                    statement = fetchCursorStatement;
                }
            }

            return statement;
        }

        private DeclareCursorStatement ParseDeclareCursor(Declare_cursorContext node)
        {
            var statement = new DeclareCursorStatement
            {
                CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName }
            };

            var cursor = node.declare_set_cursor_common();

            Select_statementContext select = null;

            if (cursor != null)
            {
                select = cursor.select_statement_standalone()?.select_statement();
            }
            else
            {
                select = node.select_statement_standalone()?.select_statement();
            }

            if (select != null)
            {
                statement.SelectStatement = ParseSelectStatement(select).FirstOrDefault();
            }

            return statement;
        }

        private TokenInfo ParseCondition(ParserRuleContext node)
        {
            if (node != null)
            {
                if (node is Search_conditionContext sc)
                {
                    var token = CreateToken(node);

                    var isIfCondition = node.Parent != null &&
                                        (node.Parent is If_statementContext || node.Parent is While_statementContext);

                    var isSearchConditionHasSubquery = IsSearchConditionHasSubquery(sc);

                    if (isIfCondition && !isSearchConditionHasSubquery)
                    {
                        token.Type = TokenType.IfCondition;
                    }
                    else
                    {
                        token.Type = TokenType.SearchCondition;
                    }

                    if (token.Type == TokenType.SearchCondition)
                    {
                        AddChildTableAndColumnNameToken(sc, token);
                    }

                    return token;
                }

                if (node is Switch_search_condition_sectionContext)
                {
                    var token = CreateToken(node, TokenType.SearchCondition);

                    return token;
                }
            }

            return null;
        }

        private RaiseErrorStatement ParseRaiseErrorStatement(Raiseerror_statementContext node)
        {
            var statement = new RaiseErrorStatement();

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl tni)
                {
                    var text = tni.GetText();

                    if (text.EndsWith("\'"))
                    {
                        statement.Content = new TokenInfo(tni);
                    }
                    else if (text.StartsWith("@"))
                    {
                        statement.Content = new TokenInfo(tni) { Type = TokenType.VariableName };
                    }
                }
                else if (child is Constant_LOCAL_IDContext clid)
                {
                    if (string.IsNullOrEmpty(statement.Severity))
                    {
                        statement.Severity = clid.GetText();
                    }
                    else if (string.IsNullOrEmpty(statement.State))
                    {
                        statement.State = clid.GetText();
                    }
                }
            }

            return statement;
        }

        private GotoStatement ParseGotoStatement(Goto_statementContext node)
        {
            var statement = new GotoStatement
            {
                Label = new TokenInfo(node.id_())
            };

            return statement;
        }

        private CreateTableStatement ParseCreateTableStatement(Create_tableContext node1)
        {
            var statement = new CreateTableStatement();
            ;

            var name = node1.table_name();
            var columns = node1.column_def_table_constraints().column_def_table_constraint();

            var tableInfo = new TableInfo
            {
                IsTemporary = IsTemporaryTable(name),
                Name = new TableName(name)
            };

            Func<IList<IParseTree>, ColumnInfo, ConstraintInfo> getConstraintInfo = (node, columnInfo) =>
            {
                var constraintInfo = new ConstraintInfo();

                foreach (var c in node)
                {
                    if (c is TerminalNodeImpl ctni)
                    {
                        var constraintType = GetConstraintType(ctni);

                        if (constraintType != ConstraintType.None)
                        {
                            constraintInfo.Type = constraintType;
                        }
                    }
                    else if (c is Id_Context cid)
                    {
                        constraintInfo.Name = new NameToken(cid);
                    }
                    else if (c is ExpressionContext exp)
                    {
                        if (constraintInfo.Type == ConstraintType.Check)
                        {
                            constraintInfo.Definition = new TokenInfo(exp);
                        }
                        else if (constraintInfo.Type == ConstraintType.Default)
                        {
                            if (columnInfo != null)
                            {
                                columnInfo.DefaultValue = new TokenInfo(exp);
                            }
                        }
                    }
                    else if (c is Column_name_list_with_orderContext cnlw)
                    {
                        if (constraintInfo.ColumnNames == null)
                        {
                            constraintInfo.ColumnNames = new List<ColumnName>();
                        }

                        constraintInfo.ColumnNames.AddRange(cnlw.id_().Select(item => new ColumnName(item)));
                    }
                    else if (c is Column_name_listContext cnl)
                    {
                        if (constraintInfo.Type == ConstraintType.ForeignKey)
                        {
                            if (constraintInfo.ForeignKey == null)
                            {
                                constraintInfo.ForeignKey = new ForeignKeyInfo();
                            }

                            constraintInfo.ForeignKey.ColumnNames.AddRange(cnl.id_()
                                .Select(item => new ColumnName(item)));
                        }
                    }
                    else if (c is Check_constraintContext check)
                    {
                        constraintInfo.Type = ConstraintType.Check;
                        constraintInfo.Definition = new TokenInfo(check.search_condition());
                    }
                    else if (c is Foreign_key_optionsContext fk)
                    {
                        if (constraintInfo.ForeignKey == null)
                        {
                            constraintInfo.ForeignKey = ParseForeignKey(fk);
                        }
                        else
                        {
                            var fki = ParseForeignKey(fk);

                            constraintInfo.ForeignKey.RefTableName = fki.RefTableName;
                            constraintInfo.ForeignKey.RefColumnNames = fki.RefColumnNames;
                        }
                    }
                }

                return constraintInfo;
            };

            foreach (var column in columns)
            {
                var columnDefiniton = column.column_definition();
                var tableConstraint = column.table_constraint();

                if (columnDefiniton != null)
                {
                    var columnInfo = new ColumnInfo
                    {
                        Name = new ColumnName(columnDefiniton.id_())
                    };

                    var isComputeExp = false;

                    foreach (var child in columnDefiniton.children)
                    {
                        if (child is TerminalNodeImpl)
                        {
                            if (child.GetText().ToUpper() == "AS")
                            {
                                isComputeExp = true;
                            }
                        }
                        else if (child is Data_typeContext dt)
                        {
                            columnInfo.DataType = new TokenInfo(dt);
                        }
                        else if (child is Column_definition_elementContext cde)
                        {
                            var text = cde.GetText().ToUpper();
                            var constraint = cde.column_constraint();

                            if (text.Contains("NOT") && text.Contains("NULL"))
                            {
                                columnInfo.IsNullable = false;
                            }
                            else if (text.StartsWith("IDENTITY"))
                            {
                                columnInfo.IsIdentity = true;

                                var index = text.IndexOf("(", StringComparison.Ordinal);

                                if (index > 0)
                                {
                                    var identityItems = text.Substring(index).Trim('(', ')').Split(',');

                                    tableInfo.IdentitySeed = int.Parse(identityItems[0].Trim());
                                    tableInfo.IdentityIncrement = int.Parse(identityItems[1].Trim());
                                }
                            }
                            else if (text.StartsWith("DEFAULT"))
                            {
                                columnInfo.DefaultValue = new TokenInfo(cde.expression());
                            }
                            else if (constraint != null)
                            {
                                var constraintInfo = getConstraintInfo(constraint.children, columnInfo);

                                if (columnInfo.Constraints == null)
                                {
                                    columnInfo.Constraints = new List<ConstraintInfo>();
                                }

                                columnInfo.Constraints.Add(constraintInfo);
                            }
                        }
                        else if (child is ExpressionContext exp)
                        {
                            if (isComputeExp)
                            {
                                columnInfo.ComputeExp = new TokenInfo(exp);

                                isComputeExp = false;
                            }
                        }
                    }

                    tableInfo.Columns.Add(columnInfo);
                }
                else if (tableConstraint != null)
                {
                    var constraintInfo = getConstraintInfo(tableConstraint.children, null);

                    if (tableInfo.Constraints == null)
                    {
                        tableInfo.Constraints = new List<ConstraintInfo>();
                    }

                    tableInfo.Constraints.Add(constraintInfo);
                }
            }

            statement.TableInfo = tableInfo;

            return statement;
        }

        private ForeignKeyInfo ParseForeignKey(Foreign_key_optionsContext node)
        {
            var fki = new ForeignKeyInfo();

            var refTableName = node.table_name();
            var refColumnNames = node.column_name_list()._col;

            fki.RefTableName = new TableName(refTableName);
            fki.RefColumnNames.AddRange(refColumnNames.Select(item => new ColumnName(item)));

            var isUpdate = false;
            var isDelete = false;
            var isCascade = false;

            foreach (var chilid in node.children)
            {
                if (chilid is TerminalNodeImpl fktni)
                {
                    var text = fktni.GetText().ToUpper();

                    if (text == "UPDATE")
                    {
                        isUpdate = true;
                    }
                    else if (text == "DELETE")
                    {
                        isDelete = true;
                    }
                    else if (text == "CASCADE")
                    {
                        isCascade = true;
                    }
                }
            }

            if (isCascade)
            {
                fki.UpdateCascade = isUpdate;
                fki.DeleteCascade = isDelete;
            }

            return fki;
        }

        private bool IsSearchConditionHasSubquery(ParserRuleContext node)
        {
            foreach (var child in node.children)
            {
                if (child is SubqueryContext)
                {
                    return true;
                }

                if (child is ParserRuleContext prc)
                {
                    return IsSearchConditionHasSubquery(prc);
                }
            }

            return false;
        }

        protected override TableName ParseTableName(ParserRuleContext node, bool strict = false)
        {
            TableName tableName = null;

            if (node != null)
            {
                if (node is Table_nameContext tn)
                {
                    tableName = new TableName(tn)
                    {
                        Database = tn.database?.GetText(),
                        Schema = tn.schema?.GetText()
                    };
                }
                else if (node is Full_table_nameContext fullName)
                {
                    tableName = new TableName(fullName)
                    {
                        Server = fullName.server?.GetText(),
                        Database = fullName.database?.GetText(),
                        Schema = fullName.schema?.GetText()
                    };

                    var parent = fullName.Parent;

                    if (parent != null && parent is Table_source_itemContext ts)
                    {
                        var alias = ts.as_table_alias();

                        if (alias != null)
                        {
                            tableName.Alias = new TokenInfo(alias.table_alias());
                        }
                    }
                }
                else if (node is Table_sourceContext ts)
                {
                    tableName = ParseTableName(ts.table_source_item_joined());
                }
                else if (node is Table_source_itemContext tsi)
                {
                    var fullTableName = tsi.full_table_name();

                    if (fullTableName != null)
                    {
                        tableName = new TableName(fullTableName);
                    }
                    else
                    {
                        tableName = new TableName(tsi);
                    }

                    var alias = tsi.as_table_alias();

                    if (tableName != null && alias != null)
                    {
                        tableName.Alias = new TokenInfo(alias.table_alias());
                    }
                }
                else if (node is Table_source_item_joinedContext tsij)
                {
                    var tsit = tsij.table_source_item();

                    if (tsit != null)
                    {
                        tableName = ParseTableName(tsit);
                    }
                    else
                    {
                        tableName = ParseTableName(tsij.table_source_item_joined());
                    }
                }
                else if (node is Table_sourcesContext tss)
                {
                    var joined = tss.table_source().FirstOrDefault()?.table_source_item_joined();
                    var tsis = joined?.table_source_item();

                    if (tsis != null)
                    {
                        tableName = ParseTableName(tsis);
                    }
                    else
                    {
                        tableName = ParseTableName(joined?.table_source_item_joined());
                    }
                }
                else if (node is Ddl_objectContext ddl)
                {
                    var fullTableName = ddl.full_table_name();

                    if (fullTableName != null)
                    {
                        return ParseTableName(fullTableName, strict);
                    }

                    tableName = new TableName(ddl);

                    if (ddl.GetText().StartsWith("@"))
                    {
                        tableName.AddChild(new TokenInfo(ddl) { Type = TokenType.VariableName });
                    }
                }

                if (!strict && tableName == null)
                {
                    tableName = new TableName(node);
                }
            }

            return tableName;
        }

        protected override ColumnName ParseColumnName(ParserRuleContext node, bool strict = false)
        {
            ColumnName columnName = null;

            if (node != null)
            {
                if (node is Full_column_nameContext fullName)
                {
                    columnName = new ColumnName(fullName.column_name.GetText(), fullName)
                    {
                        Server = fullName.server?.GetText(),
                        Schema = fullName.schema?.GetText()
                    };

                    if (fullName.tablename != null)
                    {
                        columnName.TableName = new TokenInfo(fullName.tablename);
                    }

                    var parent = fullName.Parent;

                    if (parent != null && parent is Column_elemContext celem)
                    {
                        var alias = celem.as_column_alias()?.column_alias();

                        if (alias != null)
                        {
                            columnName.Alias = new TokenInfo(alias);
                        }
                    }
                }
                else if (node is Column_def_table_constraintContext col)
                {
                    columnName = new ColumnName(col.column_definition().id_())
                    {
                        DataType = new TokenInfo(col.column_definition().data_type())
                            { Type = TokenType.DataType }
                    };
                }
                else if (node is Select_list_elemContext elem)
                {
                    var text = elem.GetText();

                    if (text.StartsWith("@"))
                    {
                        columnName = new ColumnName(elem);

                        AddNodeVariablesChildren(elem, columnName);

                        var exp = elem.expression();

                        if (exp != null)
                        {
                            AddChildTableAndColumnNameToken(exp, columnName);
                        }
                    }
                    else
                    {
                        var asterisk = elem.asterisk();

                        if (asterisk != null)
                        {
                            return ParseColumnName(asterisk, strict);
                        }

                        var columnEle = elem.column_elem();
                        var expEle = elem.expression_elem();

                        if (columnEle != null)
                        {
                            columnName = null;

                            var fullColumnName = columnEle.full_column_name();

                            if (fullColumnName != null)
                            {
                                columnName = new ColumnName(fullColumnName);

                                if (fullColumnName.tablename != null)
                                {
                                    columnName.TableName = new TokenInfo(fullColumnName.tablename);
                                }
                            }
                            else
                            {
                                var found = false;

                                foreach (var child in columnEle.children)
                                {
                                    if (child is TerminalNodeImpl && child.GetText().ToUpper() == "NULL")
                                    {
                                        found = true;

                                        columnName = new ColumnName("NULL");
                                        break;
                                    }
                                }

                                if (!found)
                                {
                                    columnName = new ColumnName(columnEle);
                                }
                            }

                            var alias = columnEle.as_column_alias()?.column_alias();

                            if (alias != null)
                            {
                                columnName.HasAs = HasAsFlag(alias);
                                columnName.Alias = new TokenInfo(alias);

                                AddChildTableAndColumnNameToken(columnEle, columnName);
                            }
                        }
                        else if (expEle != null)
                        {
                            columnName = ParseColumnName(expEle, strict);

                            AddChildTableAndColumnNameToken(expEle, columnName);

                            if (expEle.GetText().Contains("@"))
                            {
                                AddNodeVariablesChildren(expEle, columnName);
                            }
                        }
                    }
                }
                else if (node is AsteriskContext asterisk)
                {
                    columnName = new ColumnName(asterisk);

                    foreach (var ac in asterisk.children)
                    {
                        if (ac is TerminalNodeImpl terminalNode)
                        {
                            if (terminalNode.Symbol.Type == STAR)
                            {
                                columnName = new ColumnName(terminalNode);
                                break;
                            }
                        }
                    }

                    var tableName = asterisk.table_name();

                    if (columnName != null && tableName != null)
                    {
                        columnName.TableName = new TokenInfo(tableName);
                    }
                }
                else if (node is Expression_elemContext expElem)
                {
                    var expression = expElem.expression();

                    columnName = new ColumnName(expression);

                    var constContext = expression?.primitive_expression()?.constant();
                    var functionCall = expression?.function_call();

                    if (constContext != null)
                    {
                        columnName.IsConst = true;
                    }
                    else if (functionCall != null)
                    {
                        foreach (var c in functionCall.children)
                        {
                            if (c is Scalar_function_nameContext sfn)
                            {
                                columnName.AddChild(new NameToken(sfn.func_proc_name_server_database_schema())
                                    { Type = TokenType.FunctionName });
                                break;
                            }
                        }
                    }

                    var alias = expElem.as_column_alias()?.column_alias();

                    if (alias == null)
                    {
                        alias = expElem.column_alias();
                    }

                    if (alias != null)
                    {
                        columnName.HasAs = HasAsFlag(alias);
                        columnName.Alias = new TokenInfo(alias);
                    }
                }
                else if (node is ExpressionContext exp)
                {
                    var fullColName = exp.full_column_name();

                    if (fullColName != null)
                    {
                        return ParseColumnName(fullColName, strict);
                    }
                }
                else if (node is Column_aliasContext colAlias)
                {
                    if (colAlias.Parent != null && colAlias.Parent is Column_alias_listContext)
                    {
                        columnName = new ColumnName(colAlias.id_());

                        var parent = colAlias.Parent as Column_alias_listContext;

                        columnName.HasAs = HasAsFlag(parent._column_alias);
                        columnName.Alias = new TokenInfo(parent._column_alias);
                    }
                }

                if (!strict && columnName == null)
                {
                    columnName = new ColumnName(node);
                }
            }

            return columnName;
        }

        protected override TokenInfo ParseTableAlias(ParserRuleContext node)
        {
            if (node is Table_aliasContext alias)
            {
                return new TokenInfo(alias) { Type = TokenType.TableAlias };
            }

            return null;
        }

        protected override TokenInfo ParseColumnAlias(ParserRuleContext node)
        {
            if (node is Column_aliasContext alias)
            {
                return new TokenInfo(alias) { Type = TokenType.ColumnAlias };
            }

            return null;
        }

        private List<TokenInfo> GetNodeVariables(ParserRuleContext node)
        {
            var tokens = new List<TokenInfo>();

            foreach (var child in node.children)
            {
                var childText = child.GetText();

                if (childText.StartsWith("@"))
                {
                    if (child is TerminalNodeImpl impl)
                    {
                        tokens.Add(new TokenInfo(impl) { Type = TokenType.VariableName });
                    }
                    else
                    {
                        tokens.AddRange(GetNodeVariables(child as ParserRuleContext));
                    }
                }
                else if (child is ParserRuleContext context)
                {
                    tokens.AddRange(GetNodeVariables(context));
                }
            }

            return tokens;
        }

        private void AddNodeVariablesChildren(ParserRuleContext node, TokenInfo token)
        {
            var variables = GetNodeVariables(node);

            variables.ForEach(item => token.AddChild(item));
        }

        protected override bool IsFunction(IParseTree node)
        {
            if (node is Function_callContext || node is BUILT_IN_FUNCContext)
            {
                return true;
            }

            return false;
        }

        protected override TokenInfo ParseFunction(ParserRuleContext node)
        {
            var token = base.ParseFunction(node);

            foreach (var child in node.children)
            {
                if (child is NEXT_VALUE_FORContext nextVal)
                {
                    var ids = nextVal.table_name().id_();

                    NameToken seqName;

                    if (ids.Length == 2)
                    {
                        seqName = new NameToken(ids[1])
                        {
                            Type = TokenType.SequenceName,
                            Schema = ids[0].GetText()
                        };
                    }
                    else
                    {
                        seqName = new NameToken(ids[0]) { Type = TokenType.SequenceName };
                    }

                    token.AddChild(seqName);
                }
            }

            return token;
        }

        private bool IsTemporaryTable(ParserRuleContext node)
        {
            return node.GetText().Trim('[', ' ').StartsWith("#");
        }
    }
}