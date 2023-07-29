using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DatabaseInterpreter.Model;
using SqlAnalyser.Model;
using static PlSqlParser;

namespace SqlAnalyser.Core
{
    public class PlSqlRuleAnalyser : SqlRuleAnalyser
    {
        public PlSqlRuleAnalyser(string content) : base(content)
        {
        }

        public override IEnumerable<Type> ParseTableTypes => new List<Type> { typeof(Tableview_nameContext) };

        public override IEnumerable<Type> ParseColumnTypes => new List<Type>
            { typeof(Variable_nameContext), typeof(Column_nameContext) };

        public override IEnumerable<Type> ParseTableAliasTypes => new List<Type> { typeof(Table_aliasContext) };
        public override IEnumerable<Type> ParseColumnAliasTypes => new List<Type> { typeof(Column_aliasContext) };

        protected override Lexer GetLexer()
        {
            return new PlSqlLexer(GetCharStreamFromString());
        }

        protected override Parser GetParser(CommonTokenStream tokenStream)
        {
            return new PlSqlParser(tokenStream);
        }

        private Sql_scriptContext GetRootContext(out SqlSyntaxError error)
        {
            error = null;

            var parser = GetParser() as PlSqlParser;

            var errorListener = new SqlSyntaxErrorListener();

            parser.AddErrorListener(errorListener);

            var context = parser.sql_script();

            error = errorListener.Error;

            return context;
        }

        private bool CanIgnoreError(SqlSyntaxError error)
        {
            if (error == null) return true;

            if (error != null && error.Items.Count == 1)
            {
                var message = error.Items[0].Message;

                if (message.Contains("mismatched input '<EOF>'") && message.Contains("expecting"))
                {
                    if (message.Contains("'BEGIN'"))
                        return true;
                    if (error.Items[0].StopIndex == Content.Length - 1) return true;
                }
            }

            return false;
        }

        public override SqlSyntaxError Validate()
        {
            SqlSyntaxError error = null;

            var rootContext = GetRootContext(out error);

            return error;
        }

        private Unit_statementContext GetUnitStatementContext(out SqlSyntaxError error)
        {
            error = null;

            var rootContext = GetRootContext(out error);

            return rootContext?.unit_statement()?.FirstOrDefault();
        }

        public override AnalyseResult AnalyseCommon()
        {
            SqlSyntaxError error = null;

            var rootContext = GetRootContext(out error);

            var canIgnoreError = CanIgnoreError(error);

            if (canIgnoreError) error = null;

            var result = new AnalyseResult { Error = error };

            if ((!result.HasError || canIgnoreError) && rootContext != null)
            {
                var unitStatements = rootContext.unit_statement();

                if (unitStatements.Length > 0)
                {
                    CommonScript script = null;

                    var unitStatement = unitStatements.FirstOrDefault();

                    var proc = unitStatement.create_procedure_body();
                    var func = unitStatement.create_function_body();
                    var trigger = unitStatement.create_trigger();
                    var view = unitStatement.create_view();

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
                    else
                    {
                        script = new CommonScript();

                        foreach (var unit in unitStatements)
                        foreach (var child in unit.children)
                            if (child is Data_manipulation_language_statementsContext dmls)
                                script.Statements.AddRange(ParseDataManipulationLanguageStatement(dmls));
                            else if (child is ParserRuleContext prc) script.Statements.AddRange(ParseStatement(prc));
                    }

                    result.Script = script;

                    ExtractFunctions(script, unitStatement);
                }
            }

            return result;
        }

        public override AnalyseResult AnalyseProcedure()
        {
            SqlSyntaxError error = null;

            var unitStatement = GetUnitStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                var script = new RoutineScript { Type = RoutineType.PROCEDURE };

                var proc = unitStatement.create_procedure_body();

                if (proc != null) SetProcedureScript(script, proc);

                ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetProcedureScript(RoutineScript script, Create_procedure_bodyContext proc)
        {
            #region Name

            var name = proc.procedure_name();

            if (name.id_expression() != null)
            {
                script.Schema = name.identifier().GetText();
                script.Name = new TokenInfo(name.id_expression());
            }
            else
            {
                script.Name = new TokenInfo(name.identifier());
            }

            #endregion

            #region Parameters

            SetRoutineParameters(script, proc.parameter());

            #endregion

            #region Declare

            var declare = proc.seq_of_declare_specs();

            if (declare != null)
                script.Statements.AddRange(declare.declare_spec().Select(item => ParseDeclareStatement(item)));

            #endregion

            #region Body

            SetScriptBody(script, proc.body());

            #endregion
        }

        public override AnalyseResult AnalyseFunction()
        {
            SqlSyntaxError error = null;

            var unitStatement = GetUnitStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                var script = new RoutineScript { Type = RoutineType.FUNCTION };

                var func = unitStatement.create_function_body();

                if (func != null) SetFunctionScript(script, func);

                ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetFunctionScript(RoutineScript script, Create_function_bodyContext func)
        {
            #region Name

            var name = func.function_name();

            if (name.id_expression() != null)
            {
                script.Schema = name.identifier().GetText();
                script.Name = new TokenInfo(name.id_expression());
            }
            else
            {
                script.Name = new TokenInfo(name.identifier());
            }

            #endregion

            #region Parameters

            SetRoutineParameters(script, func.parameter());

            #endregion

            #region Declare

            var declare = func.seq_of_declare_specs();

            if (declare != null)
                script.Statements.AddRange(declare.declare_spec().Select(item => ParseDeclareStatement(item)));

            #endregion

            script.ReturnDataType = new TokenInfo(func.type_spec().GetText()) { Type = TokenType.DataType };

            #region Body

            SetScriptBody(script, func.body());

            #endregion
        }

        public override AnalyseResult AnalyseView()
        {
            SqlSyntaxError error = null;

            var unitStatement = GetUnitStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                var script = new ViewScript();

                var view = unitStatement.create_view();

                if (view != null) SetViewScript(script, view);

                ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetViewScript(ViewScript script, Create_viewContext view)
        {
            #region Name

            var name = view.tableview_name();

            if (name.id_expression() != null)
            {
                script.Schema = name.identifier().GetText();
                script.Name = new TokenInfo(name.id_expression());
            }
            else
            {
                script.Name = new TokenInfo(name.identifier());
            }

            #endregion

            #region Statement

            foreach (var child in view.children)
                if (child is Select_only_statementContext select)
                    script.Statements.Add(ParseSelectOnlyStatement(select));

            #endregion
        }

        public override AnalyseResult AnalyseTrigger()
        {
            SqlSyntaxError error = null;

            var unitStatement = GetUnitStatementContext(out error);

            var result = new AnalyseResult { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                var script = new TriggerScript();

                var trigger = unitStatement.create_trigger();

                if (trigger != null) SetTriggerScript(script, trigger);

                ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        private void SetTriggerScript(TriggerScript script, Create_triggerContext trigger)
        {
            #region Name

            var name = trigger.trigger_name();

            if (name.id_expression() != null)
            {
                script.Schema = name.identifier().GetText();
                script.Name = new TokenInfo(name.id_expression());
            }
            else
            {
                script.Name = new TokenInfo(name.identifier());
            }

            #endregion

            var simpleDml = trigger.simple_dml_trigger();

            if (simpleDml != null)
            {
                var tableName = simpleDml.dml_event_clause().tableview_name();
                script.TableName = new TableName(tableName);

                var events = simpleDml.dml_event_clause().dml_event_element();

                foreach (var evt in events)
                {
                    var triggerEvent = (TriggerEvent)Enum.Parse(typeof(TriggerEvent), evt.GetText().ToUpper());

                    script.Events.Add(triggerEvent);
                }

                foreach (var child in trigger.children)
                    if (child is TerminalNodeImpl terminalNode)
                        switch (terminalNode.Symbol.Type)
                        {
                            case BEFORE:
                                script.Time = TriggerTime.BEFORE;
                                break;
                            case AFTER:
                                script.Time = TriggerTime.AFTER;
                                break;
                            case INSTEAD:
                                script.Time = TriggerTime.INSTEAD_OF;
                                break;
                        }
            }

            var condition = trigger.trigger_when_clause()?.condition();

            if (condition != null) script.Condition = new TokenInfo(condition) { Type = TokenType.TriggerCondition };

            #region Body

            var triggerBody = trigger.trigger_body();
            var block = triggerBody.trigger_block();

            var declares = block.declare_spec();

            if (declares != null && declares.Length > 0)
                script.Statements.AddRange(declares.Select(item => ParseDeclareStatement(item)));

            SetScriptBody(script, block.body());

            #endregion
        }

        private void SetScriptBody(CommonScript script, BodyContext body)
        {
            script.Statements.AddRange(ParseBody(body));
        }

        private List<Statement> ParseBody(BodyContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Seq_of_statementsContext seq)
                    statements.AddRange(ParseSeqStatement(seq));

            if (node.exception_handler()?.Any() == true) statements.Add(ParseException(node));

            return statements;
        }

        private void SetRoutineParameters(RoutineScript script, ParameterContext[] parameters)
        {
            if (parameters != null)
                foreach (var parameter in parameters)
                {
                    var parameterInfo = new Parameter();

                    var paraName = parameter.parameter_name();

                    parameterInfo.Name = new TokenInfo(paraName) { Type = TokenType.ParameterName };

                    parameterInfo.DataType = new TokenInfo(parameter.type_spec().GetText())
                        { Type = TokenType.DataType };

                    var defaultValue = parameter.default_value_part();

                    if (defaultValue != null) parameterInfo.DefaultValue = new TokenInfo(defaultValue);

                    SetParameterType(parameterInfo, parameter.children);

                    script.Parameters.Add(parameterInfo);
                }
        }

        private List<Statement> ParseSeqStatement(Seq_of_statementsContext node)
        {
            var statements = new List<Statement>();

            GotoStatement gotoStatement = null;

            foreach (var child in node.children)
                if (child is StatementContext st)
                {
                    if (gotoStatement == null)
                        statements.AddRange(ParseStatement(st));
                    else
                        gotoStatement.Statements.AddRange(ParseStatement(st));
                }
                else if (child is Label_declarationContext labelDeclare)
                {
                    gotoStatement = ParseLabelDeclareStatement(labelDeclare);

                    statements.Add(gotoStatement);
                }

            return statements;
        }

        private ExceptionStatement ParseException(BodyContext body)
        {
            var statement = new ExceptionStatement();

            var handlers = body.exception_handler();

            if (handlers != null && handlers.Length > 0)
                foreach (var handler in handlers)
                {
                    var exceptionItem = new ExceptionItem
                    {
                        Name = new TokenInfo(handler.exception_name().First())
                    };
                    exceptionItem.Statements.AddRange(ParseSeqStatement(handler.seq_of_statements()));

                    statement.Items.Add(exceptionItem);
                }

            return statement;
        }

        private void SetParameterType(Parameter parameterInfo, IList<IParseTree> nodes)
        {
            foreach (var child in nodes)
                if (child is TerminalNodeImpl terminalNode)
                {
                    if (terminalNode.Symbol.Type == IN)
                        parameterInfo.ParameterType = ParameterType.IN;
                    else if (terminalNode.Symbol.Type == OUT)
                        parameterInfo.ParameterType = ParameterType.OUT;
                    else if (terminalNode.Symbol.Type == INOUT)
                        parameterInfo.ParameterType = ParameterType.IN | ParameterType.OUT;
                }
        }

        private List<Statement> ParseStatement(StatementContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children) statements.AddRange(ParseStatement(child));

            return statements;
        }

        private List<Statement> ParseStatement(IParseTree node)
        {
            var statements = new List<Statement>();

            Action<DatabaseObjectType, TokenType, ParserRuleContext> addDropStatement = (objType, tokenType, objName) =>
            {
                if (objName != null)
                {
                    var dropStatement = new DropStatement
                    {
                        ObjectType = objType,
                        ObjectName = new NameToken(objName) { Type = tokenType }
                    };

                    statements.Add(dropStatement);
                }
            };

            if (node is Sql_statementContext sql)
            {
                statements.AddRange(ParseSqlStatement(sql));
            }
            else if (node is Assignment_statementContext assignment)
            {
                statements.AddRange(ParseSetStatement(assignment));
            }
            else if (node is If_statementContext @if)
            {
                statements.Add(ParseIfStatement(@if));
            }
            else if (node is Case_statementContext @case)
            {
                statements.Add(ParseCaseStatement(@case));
            }
            else if (node is Loop_statementContext loop)
            {
                statements.Add(ParseLoopStatement(loop));
            }
            else if (node is Function_callContext funcCall)
            {
                var statement = ParseFunctionCallStatement(funcCall);

                if (statement != null) statements.Add(statement);
            }
            else if (node is Procedure_callContext procCall)
            {
                var statement = ParseProcedureCallStatement(procCall);

                if (statement != null) statements.Add(statement);
            }
            else if (node is Exit_statementContext exit)
            {
                statements.Add(ParseExitStatement(exit));
            }
            else if (node is BodyContext body)
            {
                statements.AddRange(ParseBody(body));
            }
            else if (node is Return_statementContext @return)
            {
                statements.Add(ParseReturnStatement(@return));
            }
            else if (node is Create_tableContext create_table)
            {
                statements.Add(ParseCreateTableStatement(create_table));
            }
            else if (node is Truncate_tableContext truncate_Table)
            {
                statements.Add(ParseTruncateTableStatement(truncate_Table));
            }
            else if (node is Drop_tableContext drop_Table)
            {
                addDropStatement(DatabaseObjectType.Table, TokenType.TableName, drop_Table.tableview_name());
            }
            else if (node is Drop_viewContext drop_View)
            {
                addDropStatement(DatabaseObjectType.View, TokenType.ViewName, drop_View.tableview_name());
            }
            else if (node is Drop_typeContext drop_Type)
            {
                addDropStatement(DatabaseObjectType.Type, TokenType.TypeName, drop_Type.type_name());
            }
            else if (node is Drop_sequenceContext drop_Sequence)
            {
                addDropStatement(DatabaseObjectType.Sequence, TokenType.SequenceName, drop_Sequence.sequence_name());
            }
            else if (node is Drop_functionContext drop_Func)
            {
                addDropStatement(DatabaseObjectType.Function, TokenType.FunctionName, drop_Func.function_name());
            }
            else if (node is Drop_procedureContext drop_Proc)
            {
                addDropStatement(DatabaseObjectType.Procedure, TokenType.ProcedureName, drop_Proc.procedure_name());
            }
            else if (node is Drop_triggerContext drop_Trigger)
            {
                addDropStatement(DatabaseObjectType.Trigger, TokenType.TriggerName, drop_Trigger.trigger_name());
            }
            else if (node is Goto_statementContext gst)
            {
                statements.Add(ParseGotoStatement(gst));
            }
            else if (node is Anonymous_blockContext anonymous)
            {
                statements.AddRange(ParseAnonymousBlock(anonymous));
            }

            return statements;
        }

        private List<Statement> ParseAnonymousBlock(Anonymous_blockContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Seq_of_declare_specsContext sd)
                {
                    var declares = sd.declare_spec();

                    if (declares != null) statements.AddRange(declares.Select(item => ParseDeclareStatement(item)));
                }
                else if (child is Seq_of_statementsContext seq)
                {
                    statements.AddRange(ParseSeqStatement(seq));
                }

            return statements;
        }

        private LoopExitStatement ParseExitStatement(Exit_statementContext node)
        {
            var statement = new LoopExitStatement();

            var condition = node.condition().GetText();

            statement.Condition = new TokenInfo(condition) { Type = TokenType.ExitCondition };

            statement.IsCursorLoopExit = condition.ToUpper().Contains("%NOTFOUND");

            return statement;
        }

        private Statement ParseFunctionCallStatement(Function_callContext node)
        {
            Statement statement;

            var name = node.routine_name();
            var args = node.function_argument()?.argument();

            var functionName = new TokenInfo(name) { Type = TokenType.FunctionName };

            var symbol = functionName.Symbol.ToUpper();

            if (symbol.IndexOf("DBMS_OUTPUT", StringComparison.OrdinalIgnoreCase) >= 0)
                statement = new PrintStatement { Content = new TokenInfo(node.function_argument()) };
            else if (symbol == "RAISE_APPLICATION_ERROR")
                statement = ParseRaiseErrorStatement(args);
            else
                statement = new CallStatement
                {
                    Name = functionName,
                    Parameters = args?.Select(item => new CallParameter { Value = new TokenInfo(item) }).ToList()
                };

            return statement;
        }

        private Statement ParseProcedureCallStatement(Procedure_callContext node)
        {
            Statement statement = null;

            var name = node.routine_name();
            var args = node.function_argument()?.argument();

            if (name.GetText().ToUpper() == "RAISE_APPLICATION_ERROR")
                statement = ParseRaiseErrorStatement(args);
            else
                statement = new CallStatement
                {
                    Name = new TokenInfo(name) { Type = TokenType.ProcedureName },
                    Parameters = args?.Select(item => new CallParameter { Value = new TokenInfo(item) }).ToList()
                };

            return statement;
        }

        private Statement ParseRaiseErrorStatement(ArgumentContext[] args)
        {
            var statement = new RaiseErrorStatement();

            if (args != null && args.Length > 0)
            {
                statement.ErrorCode = new TokenInfo(args[0]);
                statement.Content = new TokenInfo(args[1]);
            }

            return statement;
        }

        private List<Statement> ParseSqlStatement(Sql_statementContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Data_manipulation_language_statementsContext data)
                    statements.AddRange(ParseDataManipulationLanguageStatement(data));
                else if (child is Cursor_manipulation_statementsContext cursor)
                    statements.AddRange(ParseCursorManipulationtatement(cursor));
                else if (child is Execute_immediateContext execute) statements.Add(ParseExecuteImmediate(execute));

            return statements;
        }

        private CallStatement ParseExecuteImmediate(Execute_immediateContext node)
        {
            var statement = new CallStatement
            {
                IsExecuteSql = true
            };

            statement.Parameters.Add(new CallParameter { Value = new TokenInfo(node.expression()) });

            var usings = node.using_clause()?.using_element();

            if (usings != null)
                foreach (var item in usings)
                {
                    var parameter = new CallParameter
                        { Value = new TokenInfo(item.select_list_elements()) { Type = TokenType.VariableName } };

                    statement.Parameters.Add(parameter);
                }

            return statement;
        }

        private List<Statement> ParseDataManipulationLanguageStatement(
            Data_manipulation_language_statementsContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Select_statementContext select)
                    statements.Add(ParseSelectStatement(select));
                else if (child is Insert_statementContext insert)
                    statements.Add(ParseInsertStatement(insert));
                else if (child is Update_statementContext update)
                    statements.Add(ParseUpdateStatement(update));
                else if (child is Delete_statementContext delete) statements.AddRange(ParseDeleteStatement(delete));

            return statements;
        }

        private List<Statement> ParseCursorManipulationtatement(Cursor_manipulation_statementsContext node)
        {
            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Open_statementContext open)
                    statements.Add(ParseOpenCursorStatement(open));
                else if (child is Fetch_statementContext fetch)
                    statements.Add(ParseFetchCursorStatement(fetch));
                else if (child is Close_statementContext close) statements.Add(ParseCloseCursorStatement(close));

            return statements;
        }

        private OpenCursorStatement ParseOpenCursorStatement(Open_statementContext node)
        {
            var statement = new OpenCursorStatement
            {
                CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName }
            };

            return statement;
        }

        private FetchCursorStatement ParseFetchCursorStatement(Fetch_statementContext node)
        {
            var statement = new FetchCursorStatement
            {
                CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName }
            };

            statement.Variables.AddRange(node.variable_name()
                .Select(item => new TokenInfo(item) { Type = TokenType.VariableName }));

            return statement;
        }

        private CloseCursorStatement ParseCloseCursorStatement(Close_statementContext node)
        {
            var statement = new CloseCursorStatement
            {
                IsEnd = true,
                CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName }
            };

            return statement;
        }

        private InsertStatement ParseInsertStatement(Insert_statementContext node)
        {
            var statement = new InsertStatement();

            var single = node.single_table_insert();

            if (single != null)
                foreach (var child in single.children)
                    if (child is Insert_into_clauseContext into)
                    {
                        statement.TableName = ParseTableName(into.general_table_ref());

                        var columns = into.paren_column_list();

                        if (columns != null)
                            foreach (var colName in columns.column_list().column_name())
                                statement.Columns.Add(ParseColumnName(colName));
                    }
                    else if (child is Values_clauseContext values)
                    {
                        foreach (var v in values.children)
                            if (v is ExpressionsContext exp)
                                foreach (var expChild in exp.children)
                                    if (expChild is ExpressionContext value)
                                    {
                                        var valueInfo = new TokenInfo(value) { Type = TokenType.InsertValue };

                                        statement.Values.Add(valueInfo);
                                    }
                    }

            return statement;
        }

        private UpdateStatement ParseUpdateStatement(Update_statementContext node)
        {
            var statement = new UpdateStatement();

            var table = node.general_table_ref();

            statement.TableNames.Add(ParseTableName(table));

            var set = node.update_set_clause();
            var columnSets = set.column_based_update_set_clause();

            if (columnSets != null)
                foreach (var colSet in columnSets)
                {
                    ColumnName columnName = null;

                    var col = colSet.column_name();

                    if (col != null)
                    {
                        columnName = ParseColumnName(col);
                    }
                    else
                    {
                        var col2 = colSet.paren_column_list()?.column_list();

                        if (col2 != null)
                        {
                            columnName = ParseColumnName(col2);

                            AddChildColumnNameToken(col2, columnName);
                        }
                    }

                    var valueExp = colSet.expression();
                    var isSubquery = AnalyserHelper.IsSubQuery(valueExp);

                    TokenInfo value = null;
                    SelectStatement valueStatement = null;

                    if (!isSubquery && valueExp != null)
                    {
                        value = CreateToken(valueExp, TokenType.UpdateSetValue);

                        AddChildTableAndColumnNameToken(valueExp, value);
                    }
                    else
                    {
                        var subquery = colSet.subquery();

                        if (subquery != null)
                        {
                            valueStatement = ParseSubquery(subquery);
                        }
                        else
                        {
                            subquery = valueExp.logical_expression()?.unary_logical_expression()?.multiset_expression()
                                ?.relational_expression()
                                ?.compound_expression()?.concatenation()?.FirstOrDefault()?.model_expression()
                                ?.unary_expression()?.atom()?.subquery();

                            if (subquery != null) valueStatement = ParseSubquery(subquery);
                        }
                    }

                    var nv = new NameValueItem { Name = columnName };

                    if (valueStatement != null)
                        nv.ValueStatement = valueStatement;
                    else if (value != null) nv.Value = value;

                    statement.SetItems.Add(nv);
                }

            var condition = node.where_clause();

            if (condition != null) statement.Condition = ParseCondition(condition.expression());

            return statement;
        }

        private List<DeleteStatement> ParseDeleteStatement(Delete_statementContext node)
        {
            var statements = new List<DeleteStatement>();

            var statement = new DeleteStatement
            {
                TableName = ParseTableName(node.general_table_ref())
            };

            var condition = node.where_clause()?.expression();

            if (condition != null) statement.Condition = ParseCondition(condition);

            statements.Add(statement);

            return statements;
        }

        private SelectStatement ParseSelectStatement(Select_statementContext node)
        {
            var statement = new SelectStatement();

            SelectLimitInfo selectLimitInfo = null;

            foreach (var child in node.children)
                if (child is Select_only_statementContext query)
                {
                    statement = ParseSelectOnlyStatement(query);
                }
                else if (child is Offset_clauseContext offset)
                {
                    if (selectLimitInfo == null) selectLimitInfo = new SelectLimitInfo();

                    selectLimitInfo.StartRowIndex = new TokenInfo(offset.expression());
                }
                else if (child is Fetch_clauseContext fetch)
                {
                    if (selectLimitInfo == null) selectLimitInfo = new SelectLimitInfo();

                    selectLimitInfo.RowCount = new TokenInfo(fetch.expression());
                }

            if (statement != null)
                if (selectLimitInfo != null)
                    statement.LimitInfo = selectLimitInfo;

            return statement;
        }

        private SelectStatement ParseSelectOnlyStatement(Select_only_statementContext node)
        {
            var statement = new SelectStatement();

            List<WithStatement> withStatements = null;

            foreach (var child in node.children)
                if (child is SubqueryContext subquery)
                {
                    statement = ParseSubquery(subquery);
                }
                else if (child is Subquery_factoring_clauseContext factor)
                {
                    var statements = ParseSubqueryFactoringCause(factor);

                    if (statements != null)
                        withStatements = statements.Where(item => item is WithStatement)
                            .Select(item => (WithStatement)item).ToList();
                }

            if (withStatements != null) statement.WithStatements = withStatements;

            return statement;
        }

        private SelectStatement ParseSubquery(SubqueryContext node)
        {
            SelectStatement statement = null;

            var statements = new List<Statement>();

            foreach (var child in node.children)
                if (child is Subquery_basic_elementsContext basic)
                {
                    statement = ParseSubqueryBasic(basic);
                }
                else if (child is Subquery_operation_partContext operation)
                {
                    var st = ParseSubqueryOperation(operation);

                    if (st != null) statements.Add(st);
                }

            if (statement != null)
            {
                var unionStatements = statements.Where(item => item is UnionStatement)
                    .Select(item => (UnionStatement)item);

                if (unionStatements.Count() > 0) statement.UnionStatements = unionStatements.ToList();
            }

            return statement;
        }

        public List<Statement> ParseSubqueryFactoringCause(Subquery_factoring_clauseContext node)
        {
            List<Statement> statements = null;

            var isWith = false;

            foreach (var fc in node.children)
                if (fc is TerminalNodeImpl terminalNode)
                {
                    if (terminalNode.Symbol.Type == WITH) isWith = true;
                }
                else if (fc is Factoring_elementContext fe)
                {
                    if (isWith)
                    {
                        if (statements == null) statements = new List<Statement>();

                        var withStatement = new WithStatement
                        {
                            SelectStatements = new List<SelectStatement>(),
                            Name = new TableName(fe.query_name()) { Type = TokenType.General }
                        };

                        withStatement.SelectStatements.Add(ParseSubquery(fe.subquery()));

                        statements.Add(withStatement);
                    }
                }

            return statements;
        }

        private SelectStatement ParseSubqueryBasic(Subquery_basic_elementsContext node)
        {
            var statement = new SelectStatement();

            foreach (var child in node.children)
                if (child is SubqueryContext subquery)
                    statement = ParseSubquery(subquery);
                else if (child is Query_blockContext block) statement = ParseQueryBlock(block);

            return statement;
        }

        private SelectStatement ParseQueryBlock(Query_blockContext node)
        {
            var statement = new SelectStatement();

            var columnNames = new List<ColumnName>();

            var selectColumns = node.selected_list();

            foreach (var col in selectColumns.select_list_elements()) columnNames.Add(ParseColumnName(col));

            if (columnNames.Count == 0) columnNames.Add(ParseColumnName(selectColumns));

            statement.Columns = columnNames;

            statement.FromItems = ParseFromClause(node.from_clause());

            var into = node.into_clause();

            if (into != null)
            {
                statement.Intos = new List<TokenInfo>();

                foreach (var child in into.children)
                    if (child is ParserRuleContext pr)
                    {
                        var token = new TokenInfo(pr) { Type = TokenType.VariableName };

                        statement.Intos.Add(token);
                    }
            }

            var where = node.where_clause();
            var orderby = node.order_by_clause();
            var groupby = node.group_by_clause();
            var fetch = node.fetch_clause();

            if (where != null) statement.Where = ParseCondition(where.expression());

            var orderbyElements = orderby?.order_by_elements();

            if (orderbyElements != null && orderbyElements.Length > 0)
                statement.OrderBy = orderbyElements.Select(item => CreateToken(item, TokenType.OrderBy)).ToList();

            if (groupby != null)
            {
                var groupbyElements = groupby.group_by_elements();
                var having = groupby.having_clause();

                if (groupbyElements != null && groupbyElements.Length > 0)
                    foreach (var gpbElem in groupbyElements)
                    {
                        var gpb = CreateToken(gpbElem, TokenType.GroupBy);

                        statement.GroupBy.Add(gpb);

                        if (!AnalyserHelper.IsValidColumnName(gpb)) AddChildTableAndColumnNameToken(gpbElem, gpb);
                    }

                if (having != null) statement.Having = ParseCondition(having.condition());
            }

            if (fetch != null)
                statement.LimitInfo = new SelectLimitInfo { RowCount = new TokenInfo(fetch.expression()) };

            return statement;
        }

        private Statement ParseSubqueryOperation(Subquery_operation_partContext node)
        {
            Statement statement = null;

            var isUnion = false;
            var unionType = UnionType.UNION;

            foreach (var child in node.children)
                if (child is TerminalNodeImpl terminalNode)
                {
                    var type = terminalNode.Symbol.Type;

                    switch (type)
                    {
                        case TSqlParser.UNION:
                            isUnion = true;
                            break;
                        case TSqlParser.ALL:
                            unionType = UnionType.UNION_ALL;
                            break;
                    }
                }
                else if (child is Subquery_basic_elementsContext basic)
                {
                    if (isUnion)
                    {
                        var unionStatement = new UnionStatement
                        {
                            Type = unionType,
                            SelectStatement = ParseSubqueryBasic(basic)
                        };

                        statement = unionStatement;
                    }
                }

            return statement;
        }

        private List<FromItem> ParseFromClause(From_clauseContext node)
        {
            var fromItems = new List<FromItem>();

            var tableList = node.table_ref_list();
            var tables = tableList.table_ref();

            var asWhole = false;

            foreach (var table in tables)
            {
                var fromItem = new FromItem
                {
                    TableName = ParseTableName(table)
                };

                var joins = table.join_clause();
                var pivot = table.pivot_clause();
                var unpivot = table.unpivot_clause();

                if (joins != null && joins.Length > 0)
                {
                    foreach (var join in joins)
                    {
                        var joinItem = new JoinItem();

                        var joinType = join.outer_join_type();

                        string type = null;

                        var matched = false;

                        var jt = JoinType.INNER;

                        if (joinType != null)
                        {
                            type = joinType.GetText().ToUpper();

                            jt = GetJoinType(type, out matched);
                        }
                        else
                        {
                            foreach (var child in join.children)
                                if (child is TerminalNodeImpl tni)
                                {
                                    jt = GetJoinType(tni.GetText().ToUpper(), out matched);

                                    if (matched) break;
                                }
                        }

                        if (matched)
                        {
                            joinItem.Type = jt;
                            joinItem.TableName = ParseTableName(join.table_ref_aux());
                            joinItem.Condition = ParseCondition(join.join_on_part().FirstOrDefault()?.condition());

                            fromItem.JoinItems.Add(joinItem);
                        }
                        else
                        {
                            asWhole = true;
                            break;
                        }
                    }
                }
                else if (pivot != null)
                {
                    var joinItem = new JoinItem
                    {
                        Type = JoinType.PIVOT,
                        PivotItem = ParsePivot(pivot)
                    };
                    fromItem.JoinItems.Add(joinItem);
                }
                else if (unpivot != null)
                {
                    var joinItem = new JoinItem
                    {
                        Type = JoinType.UNPIVOT,
                        UnPivotItem = ParseUnPivot(unpivot)
                    };
                    fromItem.JoinItems.Add(joinItem);
                }

                fromItems.Add(fromItem);
            }

            if (asWhole)
            {
                fromItems.Clear();

                var fromItem = new FromItem
                {
                    TableName = new TableName(tableList)
                };

                AddChildTableAndColumnNameToken(tableList, fromItem.TableName);

                fromItems.Add(fromItem);
            }

            return fromItems;
        }

        private JoinType GetJoinType(string text, out bool matched)
        {
            matched = false;

            switch (text)
            {
                case nameof(INNER):
                    matched = true;
                    return JoinType.INNER;
                case nameof(LEFT):
                    matched = true;
                    return JoinType.LEFT;
                case nameof(RIGHT):
                    matched = true;
                    return JoinType.RIGHT;
                case nameof(FULL):
                    matched = true;
                    return JoinType.FULL;
                case nameof(CROSS):
                    matched = true;
                    return JoinType.CROSS;
                default:
                    matched = false;
                    return JoinType.INNER;
            }
        }

        private PivotItem ParsePivot(Pivot_clauseContext node)
        {
            var pivotItem = new PivotItem();

            var pm = node.pivot_element().FirstOrDefault();

            var function = pm.aggregate_function_name();

            pivotItem.AggregationFunctionName = new TokenInfo(function.identifier());
            pivotItem.AggregatedColumnName = CreateToken(pm.expression(), TokenType.ColumnName);
            pivotItem.ColumnName = ParseColumnName(node.pivot_for_clause().column_name());
            pivotItem.Values = node.pivot_in_clause().pivot_in_clause_element().Select(item => new TokenInfo(item))
                .ToList();

            return pivotItem;
        }

        private UnPivotItem ParseUnPivot(Unpivot_clauseContext node)
        {
            var unpivotItem = new UnPivotItem
            {
                ValueColumnName = ParseColumnName(node.column_name()),
                ForColumnName = ParseColumnName(node.pivot_for_clause().column_name()),
                InColumnNames = node.unpivot_in_clause().unpivot_in_elements()
                    .Select(item => ParseColumnName(item.column_name())).ToList()
            };

            return unpivotItem;
        }

        private List<SetStatement> ParseSetStatement(Assignment_statementContext node)
        {
            var statements = new List<SetStatement>();

            foreach (var child in node.children)
                if (child is General_elementContext element)
                {
                    var statement = new SetStatement
                    {
                        Key = new TokenInfo(element) { Type = TokenType.VariableName }
                    };

                    statements.Add(statement);
                }
                else if (child is ExpressionContext exp)
                {
                    statements.Last().Value = CreateToken(exp);
                }

            return statements;
        }

        private Statement ParseDeclareStatement(Declare_specContext node)
        {
            Statement statement = null;

            foreach (var child in node.children)
                if (child is Variable_declarationContext variable)
                {
                    var declareStatement = new DeclareVariableStatement
                    {
                        Name = new TokenInfo(variable.identifier()) { Type = TokenType.VariableName }
                    };

                    var typeSpec = variable.type_spec();
                    declareStatement.DataType = new TokenInfo(typeSpec);

                    declareStatement.IsCopyingDataType =
                        typeSpec.children.Any(item => item.GetText().ToUpper() == "%TYPE");

                    var expression = variable.default_value_part()?.expression();

                    if (expression != null) declareStatement.DefaultValue = new TokenInfo(expression);

                    statement = declareStatement;
                }
                else if (child is Cursor_declarationContext cursor)
                {
                    var declareCursorStatement = new DeclareCursorStatement
                    {
                        CursorName = new TokenInfo(cursor.identifier())
                            { Type = TokenType.CursorName },
                        SelectStatement = ParseSelectStatement(cursor.select_statement())
                    };

                    statement = declareCursorStatement;
                }

            return statement;
        }

        private IfStatement ParseIfStatement(If_statementContext node)
        {
            var statement = new IfStatement();

            var ifItem = new IfStatementItem { Type = IfStatementType.IF };
            var condition = node.condition();

            SetIfItemContion(ifItem, condition);

            ifItem.Statements.AddRange(ParseSeqStatement(node.seq_of_statements()));

            statement.Items.Add(ifItem);

            foreach (var elseif in node.elsif_part())
            {
                var elseIfItem = new IfStatementItem { Type = IfStatementType.ELSEIF };

                SetIfItemContion(elseIfItem, elseif.condition());

                elseIfItem.Statements.AddRange(ParseSeqStatement(elseif.seq_of_statements()));

                statement.Items.Add(elseIfItem);
            }

            var @else = node.else_part();

            if (@else != null)
            {
                var elseItem = new IfStatementItem { Type = IfStatementType.ELSE };

                elseItem.Statements.AddRange(ParseSeqStatement(@else.seq_of_statements()));

                statement.Items.Add(elseItem);
            }

            return statement;
        }

        private void SetIfItemContion(IfStatementItem ifItem, ConditionContext condition)
        {
            var unary = condition.expression()?.logical_expression()?.unary_logical_expression();

            if (unary != null)
            {
                var hasNot = false;

                foreach (var child in unary.children)
                    if (child is TerminalNodeImpl && child.GetText().ToUpper() == "NOT")
                    {
                        hasNot = true;
                    }
                    else if (child is Multiset_expressionContext multi)
                    {
                        var qualified = multi.relational_expression()?.compound_expression()?.concatenation()
                            ?.FirstOrDefault()
                            ?.model_expression()?.unary_expression()?.quantified_expression();

                        if (qualified != null)
                        {
                            foreach (var c in qualified.children)
                                if (c is TerminalNodeImpl && c.GetText().ToUpper() == "EXISTS")
                                {
                                    if (hasNot)
                                        ifItem.ConditionType = IfConditionType.NotExists;
                                    else
                                        ifItem.ConditionType = IfConditionType.Exists;

                                    break;
                                }

                            ifItem.CondtionStatement = ParseSelectOnlyStatement(qualified.select_only_statement());
                        }
                    }
            }

            if (ifItem.CondtionStatement == null) ifItem.Condition = ParseCondition(condition);
        }

        private CaseStatement ParseCaseStatement(Case_statementContext node)
        {
            var statement = new CaseStatement();

            var simple = node.simple_case_statement();

            if (simple != null)
            {
                statement.VariableName = new TokenInfo(simple.expression()) { Type = TokenType.VariableName };

                var whens = simple.simple_case_when_part();

                foreach (var when in whens)
                {
                    var ifItem = new IfStatementItem
                    {
                        Type = IfStatementType.IF,
                        Condition = new TokenInfo(when.expression().First()) { Type = TokenType.IfCondition }
                    };
                    ifItem.Statements.AddRange(ParseSeqStatement(when.seq_of_statements()));
                    statement.Items.Add(ifItem);
                }

                var @else = simple.case_else_part();

                if (@else != null)
                {
                    var elseItem = new IfStatementItem { Type = IfStatementType.ELSE };
                    elseItem.Statements.AddRange(ParseSeqStatement(@else.seq_of_statements()));

                    statement.Items.Add(elseItem);
                }
            }

            return statement;
        }

        private LoopStatement ParseLoopStatement(Loop_statementContext node)
        {
            var statement = new LoopStatement();

            var i = 0;

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    if (i == 0)
                    {
                        var type = terminalNode.Symbol.Type;

                        if (type == FOR)
                            statement.Type = LoopType.FOR;
                        else if (type == WHILE)
                            statement.Type = LoopType.WHILE;
                        else if (type == LOOP) statement.Type = LoopType.LOOP;
                    }
                }
                else if (child is Seq_of_statementsContext seq)
                {
                    statement.Statements.AddRange(ParseSeqStatement(seq));
                }
                else if (child is ConditionContext condition)
                {
                    statement.Condition = new TokenInfo(condition) { Type = TokenType.IfCondition };
                }
                else if (child is Cursor_loop_paramContext cursor)
                {
                    var loopCursorInfo = new LoopCursorInfo();

                    var indexName = cursor.index_name();
                    var recordName = cursor.record_name();

                    if (indexName != null)
                    {
                        loopCursorInfo.IsIntegerIterate = true;
                        loopCursorInfo.IteratorName = new TokenInfo(indexName);
                        loopCursorInfo.StartValue = new TokenInfo(cursor.lower_bound());
                        loopCursorInfo.StopValue = new TokenInfo(cursor.upper_bound());
                    }
                    else if (recordName != null)
                    {
                        loopCursorInfo.IteratorName = new TokenInfo(recordName);
                        loopCursorInfo.SelectStatement = ParseSelectStatement(cursor.select_statement());
                    }

                    foreach (var c in cursor.children)
                        if (c is TerminalNodeImpl tni)
                            if (c.GetText().ToUpper() == "REVERSE")
                            {
                                loopCursorInfo.IsReverse = true;
                                break;
                            }

                    statement.LoopCursorInfo = loopCursorInfo;
                }

                i++;
            }

            return statement;
        }

        private Statement ParseReturnStatement(Return_statementContext node)
        {
            Statement statement = new ReturnStatement();

            var expressioin = node.expression();

            if (expressioin != null)
                statement = new ReturnStatement { Value = new TokenInfo(expressioin) };
            else
                statement = new LeaveStatement { Content = new TokenInfo(node) };

            return statement;
        }

        private TransactionStatement ParseTransactionStatement(Transaction_control_statementsContext node)
        {
            var statement = new TransactionStatement
            {
                Content = new TokenInfo(node)
            };

            if (node.set_transaction_command() != null)
                statement.CommandType = TransactionCommandType.SET;
            else if (node.commit_statement() != null)
                statement.CommandType = TransactionCommandType.COMMIT;
            else if (node.rollback_statement() != null) statement.CommandType = TransactionCommandType.ROLLBACK;

            return statement;
        }

        private GotoStatement ParseLabelDeclareStatement(Label_declarationContext node)
        {
            var statement = new GotoStatement
            {
                Label = new TokenInfo(node.label_name())
            };

            return statement;
        }

        private GotoStatement ParseGotoStatement(Goto_statementContext node)
        {
            var statement = new GotoStatement
            {
                Label = new TokenInfo(node.label_name())
            };

            return statement;
        }

        private CreateTableStatement ParseCreateTableStatement(Create_tableContext node)
        {
            var statement = new CreateTableStatement();

            var tableInfo = new TableInfo();

            foreach (var child in node.children)
                if (child is TerminalNodeImpl tni)
                {
                    var text = tni.GetText().ToUpper();

                    if (text == "TEMPORARY")
                        tableInfo.IsTemporary = true;
                    else if (text == "PRIVATE") tableInfo.IsGlobal = false;
                }

            tableInfo.Name = new TableName(node.tableview_name());

            var columns = node.relational_table().relational_property();

            foreach (var column in columns)
            {
                var columnInfo = new ColumnInfo();

                var columnDefinition = column.column_definition();
                var virtualColumnDefition = column.virtual_column_definition();

                if (columnDefinition != null || virtualColumnDefition != null)
                {
                    var columnName = columnDefinition != null
                        ? columnDefinition.column_name()
                        : virtualColumnDefition.column_name();
                    var dataType = columnDefinition != null
                        ? columnDefinition.datatype()
                        : virtualColumnDefition.datatype();

                    columnInfo.Name = new ColumnName(columnName);
                    columnInfo.DataType = new TokenInfo(dataType) { Type = TokenType.DataType };

                    var isDefault = false;

                    foreach (var child in columnDefinition != null
                                 ? columnDefinition.children
                                 : virtualColumnDefition.children)
                    {
                        var text = child.GetText().ToUpper();

                        if (child is Autogenerated_sequence_definitionContext)
                        {
                            columnInfo.IsIdentity = true;
                        }
                        else if (child is Inline_constraintContext ic)
                        {
                            if (text.Contains("NOT") && text.Contains("NULL"))
                            {
                                columnInfo.IsNullable = false;
                            }
                            else
                            {
                                ConstraintInfo constraintInfo = null;

                                var constraintName = ic.constraint_name();

                                foreach (var c in ic.children)
                                    if (c is TerminalNodeImpl tni)
                                    {
                                        var constraintType = GetConstraintType(tni);

                                        if (constraintType != ConstraintType.None)
                                        {
                                            constraintInfo = new ConstraintInfo
                                            {
                                                Type = constraintType
                                            };

                                            break;
                                        }
                                    }
                                    else if (c is Check_constraintContext check)
                                    {
                                        constraintInfo = new ConstraintInfo
                                        {
                                            Type = ConstraintType.Check,
                                            Definition = new TokenInfo(check.condition())
                                        };
                                    }

                                if (constraintInfo != null)
                                {
                                    if (constraintName != null) constraintInfo.Name = new NameToken(constraintName);

                                    var references = ic.references_clause();

                                    if (constraintInfo.Type == ConstraintType.ForeignKey && references != null)
                                        constraintInfo.ForeignKey = ParseForeignKeyReferences(references);

                                    if (columnInfo.Constraints == null)
                                        columnInfo.Constraints = new List<ConstraintInfo>();

                                    columnInfo.Constraints.Add(constraintInfo);
                                }
                            }
                        }
                        else if (child is TerminalNodeImpl)
                        {
                            if (text == "DEFAULT") isDefault = true;
                        }
                        else if (child is ExpressionContext exp)
                        {
                            if (isDefault)
                            {
                                columnInfo.DefaultValue = new TokenInfo(exp);

                                isDefault = false;
                            }
                        }
                    }

                    tableInfo.Columns.Add(columnInfo);
                }
                else
                {
                    var constraint = column.out_of_line_constraint();

                    if (constraint != null)
                    {
                        if (tableInfo.Constraints == null) tableInfo.Constraints = new List<ConstraintInfo>();

                        var constaintName = constraint.constraint_name();

                        var constraintInfo = new ConstraintInfo
                        {
                            Name = new NameToken(constaintName)
                        };

                        foreach (var child in constraint.children)
                            if (child is TerminalNodeImpl tni)
                            {
                                var constraintType = GetConstraintType(tni);

                                if (constraintType != ConstraintType.None) constraintInfo.Type = constraintType;
                            }
                            else if (child is Column_nameContext cn)
                            {
                                if (constraintInfo.ColumnNames == null)
                                    constraintInfo.ColumnNames = new List<ColumnName>();

                                constraintInfo.ColumnNames.Add(new ColumnName(cn));
                            }
                            else if (child is ConditionContext condition)
                            {
                                if (constraintInfo.Type == ConstraintType.Check)
                                    constraintInfo.Definition = new TokenInfo(condition);
                            }
                            else if (child is Foreign_key_clauseContext fk)
                            {
                                constraintInfo.ForeignKey = ParseForeignKey(fk);
                            }

                        tableInfo.Constraints.Add(constraintInfo);
                    }
                }
            }

            statement.TableInfo = tableInfo;

            return statement;
        }

        private ForeignKeyInfo ParseForeignKey(Foreign_key_clauseContext node)
        {
            var fki = new ForeignKeyInfo();

            var columns = node.paren_column_list().column_list().column_name();
            var references = node.references_clause();

            var refTable = ParseForeignKeyReferences(references);

            fki.ColumnNames.AddRange(columns.Select(item => new ColumnName(item)));

            fki.RefTableName = refTable.RefTableName;
            fki.RefColumnNames = refTable.RefColumnNames;

            return fki;
        }

        private ForeignKeyInfo ParseForeignKeyReferences(References_clauseContext node)
        {
            var fki = new ForeignKeyInfo();

            var refTableName = node.tableview_name();
            var refColumns = node.paren_column_list().column_list().column_name();

            fki.RefTableName = new TableName(refTableName);
            fki.RefColumnNames.AddRange(refColumns.Select(item => new ColumnName(item)));

            return fki;
        }

        private TruncateStatement ParseTruncateTableStatement(Truncate_tableContext node)
        {
            var statement = new TruncateStatement
            {
                TableName = ParseTableName(node.tableview_name())
            };

            return statement;
        }

        protected override TableName ParseTableName(ParserRuleContext node, bool strict = false)
        {
            TableName tableName = null;

            Action<Table_aliasContext> setAlias = alias =>
            {
                if (tableName != null && alias != null)
                {
                    tableName.HasAs = HasAsFlag(alias);
                    tableName.Alias = new TokenInfo(alias);
                }
            };

            if (node != null)
            {
                if (node is Tableview_nameContext tv)
                {
                    tableName = new TableName(tv);

                    var parent = tv.Parent?.Parent?.Parent;

                    if (parent != null && parent is Table_ref_auxContext tra)
                    {
                        var alias = tra.table_alias();

                        if (alias != null) tableName.Alias = new TokenInfo(alias);
                    }
                }
                else if (node is Table_ref_aux_internal_oneContext traio)
                {
                    tableName = new TableName(traio);

                    var parent = traio.Parent;

                    if (parent != null && parent is Table_ref_auxContext trau)
                    {
                        var alias = trau.table_alias();

                        if (alias != null) tableName.Alias = new TokenInfo(alias);
                    }
                }
                else if (node is General_table_refContext gtr)
                {
                    tableName = new TableName(gtr.dml_table_expression_clause().tableview_name());

                    setAlias(gtr.table_alias());
                }
                else if (node is Table_ref_auxContext tra)
                {
                    var tfa = tra.table_ref_aux_internal();

                    tableName = new TableName(tfa);

                    setAlias(tra.table_alias());

                    if (AnalyserHelper.IsSubQuery(tfa)) AddChildTableAndColumnNameToken(tfa, tableName);
                }
                else if (node is Table_ref_listContext trl)
                {
                    return ParseTableName(trl.table_ref().FirstOrDefault());
                }
                else if (node is Table_refContext tr)
                {
                    return ParseTableName(tr.table_ref_aux());
                }

                if (!strict && tableName == null) tableName = new TableName(node);
            }

            return tableName;
        }

        protected override ColumnName ParseColumnName(ParserRuleContext node, bool strict = false)
        {
            ColumnName columnName = null;

            if (node != null)
            {
                if (node is Column_nameContext cn)
                {
                    columnName = new ColumnName(cn);
                }
                else if (node is Variable_nameContext vname)
                {
                    columnName = new ColumnName(vname);

                    var ids = vname.id_expression();

                    if (ids.Length > 1) columnName.TableName = new TableName(ids[0]);

                    var sle = FindSelectListEelementsContext(vname);

                    var alias = sle?.column_alias()?.identifier();

                    if (alias != null) columnName.Alias = new TokenInfo(alias);
                }
                else if (node is Select_list_elementsContext ele)
                {
                    columnName = null;

                    var tableName = ele.tableview_name();
                    var expression = ele.expression();
                    var alias = ele.column_alias();

                    if (expression != null)
                    {
                        columnName = new ColumnName(expression);

                        if (HasFunction(expression)) AddChildColumnNameToken(expression, columnName);
                    }
                    else
                    {
                        columnName = new ColumnName(ele);
                    }

                    if (tableName != null) columnName.TableName = new TokenInfo(tableName);

                    if (alias != null)
                    {
                        columnName.HasAs = HasAsFlag(alias);
                        columnName.Alias = new TokenInfo(alias.identifier());
                    }
                }
                else if (node is General_element_partContext gele)
                {
                    if (IsChildOfType<Select_list_elementsContext>(gele))
                    {
                        var ids = gele.id_expression();

                        if (ids != null && ids.Length > 0)
                        {
                            if (ids.Length > 1)
                                columnName = new ColumnName(ids[1]);
                            else
                                columnName = new ColumnName(ids[0]);
                        }
                    }
                }

                if (!strict && columnName == null) columnName = new ColumnName(node);
            }

            return columnName;
        }

        private Select_list_elementsContext FindSelectListEelementsContext(ParserRuleContext node)
        {
            if (node != null)
            {
                if (node.Parent != null && node.Parent is Select_list_elementsContext sle)
                    return sle;
                if (node.Parent != null && node.Parent is ParserRuleContext parent)
                    return FindSelectListEelementsContext(parent);
            }

            return null;
        }

        protected override TokenInfo ParseTableAlias(ParserRuleContext node)
        {
            if (node is Table_aliasContext alias)
                return new TokenInfo(alias.identifier()) { Type = TokenType.TableAlias };

            return null;
        }

        protected override TokenInfo ParseColumnAlias(ParserRuleContext node)
        {
            if (node is Column_aliasContext alias)
                return new TokenInfo(alias.identifier()) { Type = TokenType.ColumnAlias };

            return null;
        }

        private TokenInfo ParseCondition(ParserRuleContext node)
        {
            if (node != null)
                if (node is ConditionContext ||
                    node is Where_clauseContext ||
                    node is ExpressionContext)
                {
                    var token = CreateToken(node);

                    var isIfCondition = node.Parent != null &&
                                        (node.Parent is If_statementContext || node.Parent is Loop_statementContext);

                    token.Type = isIfCondition ? TokenType.IfCondition : TokenType.SearchCondition;

                    if (!isIfCondition) AddChildTableAndColumnNameToken(node, token);

                    return token;
                }

            return null;
        }

        protected override bool IsFunction(IParseTree node)
        {
            if (node is Standard_functionContext) return true;

            if (node is General_element_partContext context &&
                context.children.Any(item => item is Function_argumentContext))
                return true;

            if (node is General_element_partContext gep)
            {
                if (gep.id_expression().Last().GetText().ToUpper() == "NEXTVAL") return true;
            }
            else if (node is Variable_nameContext vn)
            {
                var text = vn.id_expression().Last().GetText().ToUpper();

                if (text == "NEXTVAL") return true;
            }
            else if (node is Non_reserved_keywords_pre12cContext n)
            {
                var parent = node.Parent?.Parent?.Parent;

                if (parent is Variable_nameContext v)
                    if (!(n.Start.StartIndex == v.Start.StartIndex && n.Stop.StopIndex == v.Stop.StopIndex))
                        return true;
            }

            return false;
        }

        protected override TokenInfo ParseFunction(ParserRuleContext node)
        {
            var token = base.ParseFunction(node);

            if (node is General_element_partContext || node is Variable_nameContext)
            {
                Id_expressionContext[] ids = null;

                if (node is General_element_partContext gep)
                    ids = gep.id_expression();
                else if (node is Variable_nameContext vn) ids = vn.id_expression();

                if (ids.Last().GetText().ToUpper() == "NEXTVAL")
                {
                    NameToken seqName;

                    if (ids.Length == 3)
                        seqName = new NameToken(ids[1])
                        {
                            Type = TokenType.SequenceName,
                            Schema = ids[0].GetText()
                        };
                    else
                        seqName = new NameToken(ids[0]) { Type = TokenType.SequenceName };

                    token.AddChild(seqName);
                }
            }

            return token;
        }

        private bool HasFunction(ParserRuleContext node)
        {
            if (node == null) return false;

            foreach (var child in node.children)
                if (IsFunction(child))
                    return true;
                else
                    return HasFunction(child as ParserRuleContext);

            return false;
        }

        private bool IsChildOfType<T>(RuleContext node)
        {
            if (node == null || node.Parent == null) return false;

            if (node.Parent != null && node.Parent.GetType() == typeof(T))
                return true;
            return IsChildOfType<T>(node.Parent);
        }
    }
}