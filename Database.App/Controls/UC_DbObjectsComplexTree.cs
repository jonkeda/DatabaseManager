using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using DatabaseManager.Profile;
using View = DatabaseInterpreter.Model.View;

namespace DatabaseManager.Controls;

public delegate void ShowDbObjectContentHandler(DatabaseObjectDisplayInfo content);

public partial class UC_DbObjectsComplexTree : UserControl, IObserver<FeedbackInfo>
{
    private ConnectionInfo connectionInfo;
    public FeedbackHandler OnFeedback;

    public ShowDbObjectContentHandler OnShowContent;

    private readonly DbInterpreterOption simpleInterpreterOption = new()
        { ObjectFetchMode = DatabaseObjectFetchMode.Simple, ThrowExceptionWhenErrorOccurs = true };

    public UC_DbObjectsComplexTree()
    {
        InitializeComponent();

        FormEventCenter.OnRefreshNavigatorFolder += RefreshFolderNode;

        CheckForIllegalCrossThreadCalls = false;
        CheckForIllegalCrossThreadCalls = false;
    }

    public DatabaseType DatabaseType { get; private set; }

    public void OnNext(FeedbackInfo value)
    {
        Feedback(value);
    }

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }

    public async Task LoadTree(DatabaseType dbType, ConnectionInfo connectionInfo)
    {
        DatabaseType = dbType;
        this.connectionInfo = connectionInfo;

        tvDbObjects.Nodes.Clear();

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, simpleInterpreterOption);

        var databases = await dbInterpreter.GetDatabasesAsync();

        var visibilities = Enumerable.Empty<DatabaseVisibilityInfo>();

        var isFileConnection = ManagerUtil.IsFileConnection(dbType);

        if (!isFileConnection && dbType != DatabaseType.Oracle)
        {
            var profileInfo = await AccountProfileManager.GetProfile(dbType.ToString(), connectionInfo.Server,
                connectionInfo.Port, connectionInfo.IntegratedSecurity, connectionInfo.UserId);

            if (profileInfo != null) visibilities = await DatabaseVisibilityManager.GetVisibilities(profileInfo.Id);
        }

        foreach (var database in databases)
        {
            if (visibilities.Any(item =>
                    item.Visible == false && item.Database.ToUpper() == database.Name.ToUpper())) continue;

            var node = DbObjectsTreeHelper.CreateTreeNode(database, true);

            if (ManagerUtil.IsFileConnection(dbType))
            {
                var profile =
                    await FileConnectionProfileManager.GetProfileByDatabase(dbType.ToString(), connectionInfo.Database);

                if (profile != null) node.Text = profile.Name;
            }

            tvDbObjects.Nodes.Add(node);
        }

        if (tvDbObjects.Nodes.Count == 1)
        {
            tvDbObjects.SelectedNode = tvDbObjects.Nodes[0];
            tvDbObjects.Nodes[0].Expand();
        }
    }

    public void ClearNodes()
    {
        tvDbObjects.Nodes.Clear();
    }

    private void tvDbObjects_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            if (e.Node.Name == DbObjectsTreeHelper.FakeNodeName) return;

            tvDbObjects.SelectedNode = e.Node;

            SetMenuItemVisible(e.Node);

            contextMenuStrip1.Show(Cursor.Position);
        }
    }

    private bool CanRefresh(TreeNode node)
    {
        return node.Level <= 3 && !IsOnlyHasFakeChild(node)
                               && !(node.Tag is ScriptDbObject && !(node.Tag is View))
                               && !(node.Tag is UserDefinedType)
                               && !(node.Tag is Sequence);
    }

    private bool CanDelete(TreeNode node)
    {
        return node.Level == 2 || (node.Level == 4 && !(node.Tag is TableColumn));
    }

    private void SetMenuItemVisible(TreeNode node)
    {
        var isDatabase = node.Level == 0;
        var isView = node.Tag is View;
        var isTable = node.Tag is Table;
        var isScriptObject = node.Tag is ScriptDbObject;
        var isUserDefinedType = node.Tag is UserDefinedType;
        var isSequence = node.Tag is Sequence;
        var isFunction = node.Tag is Function;
        var isTriggerFunction = isFunction && (node.Tag as Function).IsTriggerFunction;
        var isProcedure = node.Tag is Procedure;

        tsmiNewQuery.Visible = isDatabase;
        tsmiNewTable.Visible = node.Name == nameof(DbObjectTreeFolderType.Tables) || isTable;
        tsmiNewView.Visible = node.Name == nameof(DbObjectTreeFolderType.Views) || isView;
        tsmiNewFunction.Visible = node.Name == nameof(DbObjectTreeFolderType.Functions) || node.Tag is Function;
        tsmiNewProcedure.Visible = node.Name == nameof(DbObjectTreeFolderType.Procedures) || node.Tag is Procedure;
        tsmiNewTrigger.Visible = node.Name == nameof(DbObjectTreeFolderType.Triggers) || node.Tag is TableTrigger;
        tsmiAlter.Visible = isScriptObject;
        tsmiDesign.Visible = isTable;
        tsmiCopy.Visible = isTable;
        tsmiRefresh.Visible = CanRefresh(node);
        tsmiGenerateScripts.Visible = isDatabase || isTable || isScriptObject || isUserDefinedType || isSequence;
        tsmiConvert.Visible = isDatabase;
        tsmiEmptyDatabase.Visible = isDatabase;
        tsmiDelete.Visible = CanDelete(node);
        tsmiViewData.Visible = isTable || isView;
        tsmiTranslate.Visible = isTable || isUserDefinedType || isSequence || isScriptObject;
        tsmiMore.Visible = isDatabase;
        tsmiBackup.Visible = isDatabase;
        tsmiDiagnose.Visible = isDatabase;
        tsmiCompare.Visible = isDatabase;

        tsmiSelectScript.Visible = isTable || isView || (isFunction && !isTriggerFunction);
        tsmiInsertScript.Visible = isTable;
        tsmiUpdateScript.Visible = isTable;
        tsmiDeleteScript.Visible = isTable;
        tsmiViewDependency.Visible = isDatabase ||
                                     ((isTable || isView || isFunction || isProcedure) &&
                                      DatabaseType != DatabaseType.Sqlite);
        tsmiExecuteScript.Visible = isProcedure;

        tsmiCopyChildrenNames.Visible = node.Level == 1 && node.Nodes.Count > 0 && node.Nodes[0].Tag != null;
    }

    private ConnectionInfo GetConnectionInfo(string database)
    {
        var info = ObjectHelper.CloneObject<ConnectionInfo>(connectionInfo);
        info.Database = database;
        return info;
    }

    public ConnectionInfo GetCurrentConnectionInfo()
    {
        var node = tvDbObjects.SelectedNode;

        if (node != null)
        {
            var dbNode = GetDatabaseNode(node);
            var connectionInfo = GetConnectionInfo(dbNode.Name);

            return connectionInfo;
        }

        return null;
    }

    private bool IsOnlyHasFakeChild(TreeNode node)
    {
        if (node.Nodes.Count == 1 && node.Nodes[0].Name == DbObjectsTreeHelper.FakeNodeName) return true;

        return false;
    }

    private TreeNode GetDatabaseNode(TreeNode node)
    {
        while (!(node.Tag is Database)) return GetDatabaseNode(node.Parent);
        return node;
    }

    private DbInterpreter GetDbInterpreter(string database, bool isSimpleMode = true)
    {
        var connectionInfo = GetConnectionInfo(database);

        var option = isSimpleMode
            ? simpleInterpreterOption
            : new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Details };

        option.ThrowExceptionWhenErrorOccurs = false;

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, option);

        return dbInterpreter;
    }

    private async Task AddDbObjectNodes(TreeNode parentNode, string database,
        DatabaseObjectType databaseObjectType = DatabaseObjectType.None, bool createFolderNode = true)
    {
        var dbInterpreter = GetDbInterpreter(database);

        var filter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };

        if (DatabaseType == DatabaseType.Oracle) filter.Schema = database;

        var schemaInfo = databaseObjectType == DatabaseObjectType.None
            ? new SchemaInfo()
            : await dbInterpreter.GetSchemaInfoAsync(filter);

        ClearNodes(parentNode);

        if (databaseObjectType == DatabaseObjectType.Table)
            AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Table, schemaInfo.Tables, createFolderNode,
                true);

        if (databaseObjectType == DatabaseObjectType.View)
            AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.View, schemaInfo.Views, createFolderNode,
                true);

        if (databaseObjectType == DatabaseObjectType.Function)
            AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Function, schemaInfo.Functions,
                createFolderNode);

        if (databaseObjectType == DatabaseObjectType.Procedure)
            AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Procedure, schemaInfo.Procedures,
                createFolderNode);

        if (databaseObjectType == DatabaseObjectType.Type)
            foreach (var userDefinedType in schemaInfo.UserDefinedTypes)
            {
                var dataType = userDefinedType.Attributes.Count > 1 ? "" : userDefinedType.Attributes.First().DataType;
                var strDataType = string.IsNullOrEmpty(dataType) ? "" : $"({dataType})";

                var text = $"{userDefinedType.Name}{strDataType}";

                var imageKeyName = nameof(userDefinedType);

                var node = DbObjectsTreeHelper.CreateTreeNode(userDefinedType.Name, text, imageKeyName);
                node.Tag = userDefinedType;

                parentNode.Nodes.Add(node);
            }

        if (databaseObjectType == DatabaseObjectType.Sequence)
            AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Sequence, schemaInfo.Sequences,
                createFolderNode);
    }

    private TreeNodeCollection AddTreeNodes<T>(TreeNode node, DatabaseObjectType types, DatabaseObjectType type,
        List<T> dbObjects, bool createFolderNode = true, bool createFakeNode = false)
        where T : DatabaseObject
    {
        var targetNode = node;

        if (types.HasFlag(type))
        {
            if (createFolderNode)
                targetNode = node.AddDbObjectFolderNode(dbObjects);
            else
                targetNode = node.AddDbObjectNodes(dbObjects);
        }

        if (createFakeNode && targetNode != null)
            foreach (TreeNode child in targetNode.Nodes)
                child.Nodes.Add(DbObjectsTreeHelper.CreateFakeNode());

        return node.Nodes;
    }

    private void AddTableFakeNodes(TreeNode tableNode, Table table)
    {
        ClearNodes(tableNode);

        tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Columns),
            nameof(DbObjectTreeFolderType.Columns), true));
        tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Triggers),
            nameof(DbObjectTreeFolderType.Triggers), true));
        tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Indexes),
            nameof(DbObjectTreeFolderType.Indexes), true));
        tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Keys),
            nameof(DbObjectTreeFolderType.Keys), true));
        tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Constraints),
            nameof(DbObjectTreeFolderType.Constraints), true));
    }

    private void AddViewFakeNodes(TreeNode viewNode, View view)
    {
        ClearNodes(viewNode);

        viewNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Columns),
            nameof(DbObjectTreeFolderType.Columns), true));

        if (DatabaseType == DatabaseType.SqlServer || DatabaseType == DatabaseType.Sqlite)
            viewNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Triggers),
                nameof(DbObjectTreeFolderType.Triggers), true));

        if (DatabaseType == DatabaseType.SqlServer)
            viewNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Indexes),
                nameof(DbObjectTreeFolderType.Indexes), true));
    }

    private void AddDatabaseFakeNodes(TreeNode databaseNode, Database database)
    {
        ClearNodes(databaseNode);

        var dbInterpreter = GetDbInterpreter(database.Name);

        var supportDbObjectType = dbInterpreter.SupportDbObjectType;

        databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Tables),
            nameof(DbObjectTreeFolderType.Tables), true));
        databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Views),
            nameof(DbObjectTreeFolderType.Views), true));

        if (supportDbObjectType.HasFlag(DatabaseObjectType.Function))
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Functions),
                nameof(DbObjectTreeFolderType.Functions), true));

        if (supportDbObjectType.HasFlag(DatabaseObjectType.Procedure))
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Procedures),
                nameof(DbObjectTreeFolderType.Procedures), true));

        if (supportDbObjectType.HasFlag(DatabaseObjectType.Type))
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Types),
                nameof(DbObjectTreeFolderType.Types), true));

        if (supportDbObjectType.HasFlag(DatabaseObjectType.Sequence))
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Sequences),
                nameof(DbObjectTreeFolderType.Sequences), true));
    }

    private async Task AddTableObjectNodes(TreeNode treeNode, Table table, DatabaseObjectType databaseObjectType,
        bool isForView = false)
    {
        var nodeName = treeNode.Name;
        var database = GetDatabaseNode(treeNode).Name;
        var dbInterpreter = GetDbInterpreter(database, false);

        dbInterpreter.Subscribe(this);

        var filter = new SchemaInfoFilter
        {
            Strict = true, DatabaseObjectType = databaseObjectType, Schema = table.Schema,
            TableNames = new[] { table.Name }
        };

        if (isForView)
        {
            filter.ColumnType = ColumnType.ViewColumn;
            filter.IsForView = true;
        }

        var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

        ClearNodes(treeNode);

        #region Columns

        if (nodeName == nameof(DbObjectTreeFolderType.Columns))
            foreach (var column in schemaInfo.TableColumns)
            {
                var text = GetColumnText(dbInterpreter, table, column);
                var isPrimaryKey =
                    schemaInfo.TablePrimaryKeys.Any(item => item.Columns.Any(t => t.ColumnName == column.Name));
                var isForeignKey =
                    schemaInfo.TableForeignKeys.Any(item => item.Columns.Any(t => t.ColumnName == column.Name));
                var imageKeyName = isPrimaryKey ? nameof(TablePrimaryKey) :
                    isForeignKey ? nameof(TableForeignKey) : nameof(TableColumn);

                var node = DbObjectsTreeHelper.CreateTreeNode(column.Name, text, imageKeyName);
                node.Tag = column;

                treeNode.Nodes.Add(node);
            }

        #endregion

        if (nodeName == nameof(DbObjectTreeFolderType.Triggers)) treeNode.AddDbObjectNodes(schemaInfo.TableTriggers);

        if (!isForView)
        {
            #region Indexes

            if (nodeName == nameof(DbObjectTreeFolderType.Indexes) && schemaInfo.TableIndexes.Any())
                foreach (var index in schemaInfo.TableIndexes)
                {
                    var isUnique = index.IsUnique;
                    var strColumns = string.Join(",",
                        index.Columns.OrderBy(item => item.Order).Select(item => item.ColumnName));

                    var content = index.Columns.Count > 0 ? isUnique ? $"(Unique, {strColumns})" : $"({strColumns})"
                        : isUnique ? "(Unique)" : "";

                    var text = $"{index.Name}{content}";
                    var imageKeyName = nameof(TableIndex);

                    var node = DbObjectsTreeHelper.CreateTreeNode(index.Name, text, imageKeyName);
                    node.Tag = index;

                    treeNode.Nodes.Add(node);
                }

            #endregion

            if (nodeName == nameof(DbObjectTreeFolderType.Keys))
            {
                foreach (var key in schemaInfo.TablePrimaryKeys)
                {
                    var node = DbObjectsTreeHelper.CreateTreeNode(key);

                    if (string.IsNullOrEmpty(node.Text)) node.Text = $"PK_{key.TableName}(unnamed)";

                    treeNode.Nodes.Add(node);
                }

                foreach (var key in schemaInfo.TableForeignKeys)
                {
                    var node = DbObjectsTreeHelper.CreateTreeNode(key);

                    if (string.IsNullOrEmpty(node.Text)) node.Text = $"FK_{key.TableName}(unnamed)";

                    treeNode.Nodes.Add(node);
                }
            }

            #region Constraints

            if (nodeName == nameof(DbObjectTreeFolderType.Constraints) && schemaInfo.TableConstraints.Any())
                foreach (var constraint in schemaInfo.TableConstraints)
                {
                    var node = DbObjectsTreeHelper.CreateTreeNode(constraint);
                    treeNode.Nodes.Add(node);
                }

            #endregion
        }

        Feedback("");
    }

    private string GetColumnText(DbInterpreter dbInterpreter, Table table, TableColumn column)
    {
        var text = dbInterpreter.ParseColumn(table, column).Replace(dbInterpreter.QuotationLeftChar.ToString(), "")
            .Replace(dbInterpreter.QuotationRightChar.ToString(), "");

        var index = text.IndexOf(column.Name, StringComparison.Ordinal);

        var displayText = text.Substring(index + column.Name.Length);

        return $"{column.Name} ({displayText.ToLower().Trim()})";
    }

    private async void tvDbObjects_BeforeExpand(object sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;

        if (!IsOnlyHasFakeChild(node)) return;

        tvDbObjects.BeginInvoke(async () => await LoadChildNodes(node));
    }

    private void ClearNodes(TreeNode node)
    {
        node.Nodes.Clear();
    }

    private void ShowLoading(TreeNode node)
    {
        var loadingImageKey = "Loading.gif";
        var loadingText = "loading...";

        if (IsOnlyHasFakeChild(node))
        {
            node.Nodes[0].ImageKey = loadingImageKey;
            node.Nodes[0].Text = loadingText;
        }
        else
        {
            node.Nodes.Add(DbObjectsTreeHelper.CreateTreeNode("Loading", loadingText, loadingImageKey));
        }
    }

    private async Task LoadChildNodes(TreeNode node)
    {
        ShowLoading(node);

        var tag = node.Tag;

        if (tag is Database database)
        {
            AddDatabaseFakeNodes(node, database);
        }
        else if (tag is Table table1)
        {
            AddTableFakeNodes(node, table1);
        }
        else if (tag is View view)
        {
            AddViewFakeNodes(node, view);
        }
        else if (tag == null)
        {
            var name = node.Name;

            var parentNode = node.Parent;

            if (parentNode.Tag is Database)
            {
                var databaseName = parentNode.Name;

                var databaseObjectType = DbObjectsTreeHelper.GetDbObjectTypeByFolderName(name);

                if (databaseObjectType != DatabaseObjectType.None)
                {
                    await AddDbObjectNodes(node, databaseName, databaseObjectType, false);

                    ShowChildrenCount(node);
                }
            }
            else if (parentNode.Tag is Table nodeTag)
            {
                var databaseObjectType = GetDatabaseObjectTypeByFolderNames(name);

                await AddTableObjectNodes(node, nodeTag, databaseObjectType);
            }
            else if (parentNode.Tag is View parentNodeTag)
            {
                var table = ObjectHelper.CloneObject<Table>(parentNodeTag);

                var databaseObjectType = GetDatabaseObjectTypeByFolderNames(name);

                await AddTableObjectNodes(node, table, databaseObjectType, true);
            }
        }
    }

    private DatabaseObjectType GetDatabaseObjectTypeByFolderNames(string folderName)
    {
        var databaseObjectType = DatabaseObjectType.None;

        switch (folderName)
        {
            case nameof(DbObjectTreeFolderType.Columns):
                databaseObjectType = DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey |
                                     DatabaseObjectType.ForeignKey;
                break;
            case nameof(DbObjectTreeFolderType.Triggers):
                databaseObjectType = DatabaseObjectType.Trigger;
                break;
            case nameof(DbObjectTreeFolderType.Indexes):
                databaseObjectType = DatabaseObjectType.Index;
                break;
            case nameof(DbObjectTreeFolderType.Keys):
                databaseObjectType = DatabaseObjectType.PrimaryKey | DatabaseObjectType.ForeignKey;
                break;
            case nameof(DbObjectTreeFolderType.Constraints):
                databaseObjectType = DatabaseObjectType.Constraint;
                break;
        }

        return databaseObjectType;
    }

    private void ShowChildrenCount(TreeNode node)
    {
        node.Text = $"{node.Name} ({node.Nodes.Count})";
    }

    private async void tsmiRefresh_Click(object sender, EventArgs e)
    {
        await RefreshNode();
    }

    private async Task RefreshNode()
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        if (CanRefresh(node)) await LoadChildNodes(node);
    }

    private bool IsValidSelectedNode()
    {
        var node = GetSelectedNode();

        return node != null;
    }

    private TreeNode GetSelectedNode()
    {
        return tvDbObjects.SelectedNode;
    }

    private async void GenerateScripts(ScriptAction scriptAction)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        await GenerateScripts(node, scriptAction);
    }

    private async Task GenerateScripts(TreeNode node, ScriptAction scriptAction)
    {
        var tag = node.Tag;

        if (tag is Database database)
        {
            var frmGenerateScripts = new frmGenerateScripts(DatabaseType, GetConnectionInfo(database.Name));
            frmGenerateScripts.ShowDialog();
        }
        else if (tag is DatabaseObject databaseObject)
        {
            var databaseName = GetDatabaseNode(node).Name;

            await GenerateObjectScript(databaseName, databaseObject, scriptAction);
        }
    }

    private async Task GenerateObjectScript(string database, DatabaseObject dbObj, ScriptAction scriptAction)
    {
        try
        {
            var dbInterpreter = GetDbInterpreter(database, false);

            dbInterpreter.Option.ThrowExceptionWhenErrorOccurs = true;

            var scriptGenerator = new ScriptGenerator(dbInterpreter);

            var result = await scriptGenerator.Generate(dbObj, scriptAction);

            ShowContent(new DatabaseObjectDisplayInfo
            {
                Name = dbObj.Name,
                DatabaseType = DatabaseType,
                DatabaseObject = dbObj,
                ConnectionInfo = dbInterpreter.ConnectionInfo,
                Content = result.Script,
                ScriptAction = scriptAction,
                ScriptParameters = result.Parameters
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
        }
    }

    private void tsmiConvert_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        ConvertDatabase(node);
    }

    private void ConvertDatabase(TreeNode node)
    {
        var database = node.Tag as Database;

        var frmConvert = new frmConvert(DatabaseType, GetConnectionInfo(database.Name));
        frmConvert.ShowDialog();
    }

    private async void tsmiClearData_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        if (MessageBox.Show(
                $"Are you sure to clear all data of the database?{Environment.NewLine}Please handle this operation carefully!",
                "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var node = GetSelectedNode();

            await ClearData(node.Name);
        }
    }

    private async Task ClearData(string database)
    {
        var dbInterpreter = GetDbInterpreter(database);
        var dbManager = new DbManager(dbInterpreter);

        dbManager.Subscribe(this);
        dbInterpreter.Subscribe(this);

        await dbManager.ClearData();

        if (!dbInterpreter.HasError) MessageBox.Show("Data has been cleared.");
    }

    private void Feedback(FeedbackInfo info)
    {
        OnFeedback?.Invoke(info);
    }

    private void Feedback(string message)
    {
        Feedback(new FeedbackInfo { Message = message });
    }

    private async void tsmiEmptyDatabase_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        if (MessageBox.Show(
                $"Are you sure to delete all objects of the database?{Environment.NewLine}Please handle this operation carefully!",
                "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var dbInterpreter = GetDbInterpreter((GetSelectedNode().Tag as Database).Name);

            var selector = new frmItemsSelector("Select Database Object Types",
                ItemsSelectorHelper.GetDatabaseObjectTypeItems(DatabaseType, dbInterpreter.SupportDbObjectType));

            if (selector.ShowDialog() == DialogResult.OK)
            {
                var node = GetSelectedNode();

                await EmptyDatabase(node.Name,
                    ItemsSelectorHelper.GetDatabaseObjectTypeByCheckItems(selector.CheckedItem));

                await LoadChildNodes(node);

                Feedback("");
            }
        }
    }

    private async Task EmptyDatabase(string database, DatabaseObjectType databaseObjectType)
    {
        var dbInterpreter = GetDbInterpreter(database);
        dbInterpreter.Option.ThrowExceptionWhenErrorOccurs = false;
        var dbManager = new DbManager(dbInterpreter);

        dbInterpreter.Subscribe(this);
        dbManager.Subscribe(this);

        await dbManager.EmptyDatabase(databaseObjectType);

        if (!dbInterpreter.HasError) MessageBox.Show("Seleted database objects have been deleted.");
    }

    private async void tvDbObjects_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
            await RefreshNode();
        else if (e.KeyCode == Keys.Delete) DeleteNode();

        if (e.Control)
        {
            if (e.KeyCode == Keys.F)
                FindChild();
            else if (e.KeyCode == Keys.C) CopyNodeText();
        }
    }

    private void CopyNodeText()
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        Clipboard.SetDataObject(node.Text);
    }

    private bool IsEmptyTreeNode(TreeNode node)
    {
        return node.Nodes.Count == 0 || (node.Nodes.Count == 1 && node.Nodes[0].Tag == null);
    }

    private bool IsTreeNodeHasDbObjectChildren(TreeNode node)
    {
        return !(IsEmptyTreeNode(node) ||
                 node.Nodes.Cast<TreeNode>().All(item => item.Tag == null && IsEmptyTreeNode(item)));
    }

    private void FindChild()
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        if (node.Level >= 1)
        {
            var targetNodes = IsTreeNodeHasDbObjectChildren(node) ? node.Nodes : node.Parent.Nodes;

            if (targetNodes.Count <= 1) return;

            var findBox = new frmFindBox();

            findBox.StartPosition = FormStartPosition.Manual;
            findBox.Location = new Point(0, 90);

            var result = findBox.ShowDialog();

            if (result == DialogResult.OK)
            {
                var word = findBox.FindWord;

                var foundNode = FindTreeNode(targetNodes, word);

                if (foundNode != null)
                {
                    tvDbObjects.SelectedNode = foundNode;
                    foundNode.EnsureVisible();
                }
                else
                {
                    MessageBox.Show("Not found.");
                }
            }
        }
    }

    private TreeNode FindTreeNode(TreeNodeCollection nodes, string word)
    {
        foreach (TreeNode node in nodes)
        {
            var tag = node.Tag;

            if (node.Tag != null)
            {
                var text = node.Text.Split('.').LastOrDefault()?.Split('(')?.FirstOrDefault()?.Trim();

                if (text.ToUpper() == word.ToUpper()) return node;
            }
            else if (node.Nodes.Count >= 1)
            {
                return FindTreeNode(node.Nodes, word);
            }
        }

        return null;
    }

    private void DeleteNode()
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        if (CanDelete(node))
            if (MessageBox.Show("Are you sure to delete this object?", "Confirm", MessageBoxButtons.YesNo) ==
                DialogResult.Yes)
                DropDbObject(node);
    }

    private async void DropDbObject(TreeNode node)
    {
        var database = GetDatabaseNode(node).Name;
        var dbObject = node.Tag as DatabaseObject;

        var dbInterpreter = GetDbInterpreter(database);
        dbInterpreter.Subscribe(this);

        var dbManager = new DbManager(dbInterpreter);

        dbInterpreter.Subscribe(this);
        dbManager.Subscribe(this);

        var success = await dbManager.DropDbObject(dbObject);

        if (!dbInterpreter.HasError && success)
        {
            var parentIsChildFolderOfDatabase = node.Parent?.Parent?.Tag is Database;
            var parentNode = node.Parent;

            node.Parent.Nodes.Remove(node);

            if (parentIsChildFolderOfDatabase) ShowChildrenCount(parentNode);
        }
        else
        {
            MessageBox.Show("Not drop the database object.");
        }
    }

    private void tsmiDelete_Click(object sender, EventArgs e)
    {
        DeleteNode();
    }

    private void tsmiViewData_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        ViewData(node);
    }

    private void ViewData(TreeNode node)
    {
        var database = GetDatabaseNode(node).Name;
        var dbObject = node.Tag as DatabaseObject;

        ShowContent(new DatabaseObjectDisplayInfo
        {
            Name = dbObject.Name, DatabaseType = DatabaseType, DatabaseObject = dbObject,
            DisplayType = DatabaseObjectDisplayType.Data, ConnectionInfo = GetConnectionInfo(database)
        });
    }

    private void tvDbObjects_AfterExpand(object sender, TreeViewEventArgs e)
    {
        Feedback("");
    }

    private void tsmiTranslate_MouseEnter(object sender, EventArgs e)
    {
        tsmiTranslate.DropDownItems.Clear();

        var node = GetSelectedNode();

        if (node == null || node.Tag == null) return;

        var dbObjectType = DbObjectHelper.GetDatabaseObjectType(node.Tag as DatabaseObject);

        var dbTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

        foreach (var dbType in dbTypes)
            if ((int)dbType != (int)DatabaseType)
            {
                var dbInterpreter =
                    DbInterpreterHelper.GetDbInterpreter(dbType, new ConnectionInfo(), new DbInterpreterOption());

                if (dbInterpreter.SupportDbObjectType.HasFlag(dbObjectType))
                {
                    var item = new ToolStripMenuItem(dbType.ToString());
                    item.Click += TranslateItem_Click;

                    tsmiTranslate.DropDownItems.Add(item);
                }
            }
    }

    private async void TranslateItem_Click(object sender, EventArgs e)
    {
        var dbType = ManagerUtil.GetDatabaseType((sender as ToolStripMenuItem).Text);

        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();
        tvDbObjects.SelectedNode = node;

        await Translate(node, dbType);
    }

    private async Task Translate(TreeNode node, DatabaseType targetDbType)
    {
        var tag = node.Tag;

        var connectionInfo = GetConnectionInfo((GetDatabaseNode(node).Tag as Database).Name);

        if (tag is DatabaseObject dbObject)
        {
            var translateManager = new TranslateManager();
            translateManager.Subscribe(this);

            try
            {
                var result =
                    await translateManager.Translate(DatabaseType, targetDbType, dbObject, connectionInfo, true);

                if (result != null)
                {
                    var info = new DatabaseObjectDisplayInfo
                    {
                        Schema = result.DbObjectSchema ?? dbObject.Schema,
                        Name = dbObject.Name,
                        DatabaseType = targetDbType,
                        DatabaseObject = dbObject,
                        Content = result.Data?.ToString(),
                        ConnectionInfo = null,
                        Error = result.Error,
                        IsTranlatedScript = true
                    };

                    ShowContent(info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
            }
        }
    }

    private void tvDbObjects_ItemDrag(object sender, ItemDragEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var treeNode = e.Item as TreeNode;

            if (treeNode != null && treeNode.Tag is DatabaseObject)
            {
                var text = treeNode.Text;
                var index = text.IndexOf('(');

                if (index > 0) text = text.Substring(0, index);

                var dbInterpreter = GetDbInterpreter(GetDatabaseNode(treeNode).Name);

                var items = text.Trim().Split('.').Select(item => dbInterpreter.GetQuotedString(item));

                DoDragDrop(string.Join(".", items), DragDropEffects.Move);
            }
        }
    }

    public DatabaseObjectDisplayInfo GetDisplayInfo()
    {
        var node = tvDbObjects.SelectedNode;

        var info = new DatabaseObjectDisplayInfo { DatabaseType = DatabaseType };

        if (node != null)
        {
            if (node.Tag is DatabaseObject dbObject)
            {
                info.Name = dbObject.Name;
                info.DatabaseObject = dbObject;
            }

            var databaseNode = GetDatabaseNode(node);

            if (databaseNode != null)
                info.ConnectionInfo = GetConnectionInfo(databaseNode.Name);
            else
                info.ConnectionInfo = connectionInfo;
        }

        return info;
    }

    private void tsmiNewQuery_Click(object sender, EventArgs e)
    {
        ShowContent(DatabaseObjectDisplayType.Script);
    }

    private void ShowContent(DatabaseObjectDisplayInfo info)
    {
        OnShowContent?.Invoke(info);
    }

    private void ShowContent(DatabaseObjectDisplayType displayType, bool isNew = true)
    {
        var info = new DatabaseObjectDisplayInfo
            { IsNew = isNew, DisplayType = displayType, DatabaseType = DatabaseType };

        if (!isNew)
        {
            var dbObject = tvDbObjects.SelectedNode.Tag as DatabaseObject;

            if (dbObject != null)
            {
                info.DatabaseObject = dbObject;
                info.Schema = dbObject.Schema;
                info.Name = dbObject.Name;
            }
        }

        info.ConnectionInfo = GetCurrentConnectionInfo();

        ShowContent(info);
    }

    private void tsmiNewView_Click(object sender, EventArgs e)
    {
        DoScript(DatabaseObjectType.View, ScriptAction.CREATE);
    }

    private void tsmiNewFunction_Click(object sender, EventArgs e)
    {
        DoScript(DatabaseObjectType.Function, ScriptAction.CREATE);
    }

    private void tsmiNewProcedure_Click(object sender, EventArgs e)
    {
        DoScript(DatabaseObjectType.Procedure, ScriptAction.CREATE);
    }

    private void tsmiNewTrigger_Click(object sender, EventArgs e)
    {
        DoScript(DatabaseObjectType.Trigger, ScriptAction.CREATE);
    }

    private void DoScript(DatabaseObjectType databaseObjectType, ScriptAction scriptAction)
    {
        var dbInterpreter =
            DbInterpreterHelper.GetDbInterpreter(DatabaseType, new ConnectionInfo(), new DbInterpreterOption());

        var scriptTemplate = new ScriptTemplate(dbInterpreter);

        var displayInfo = GetDisplayInfo();
        displayInfo.IsNew = true;

        DatabaseObject dbObj = null;

        if (databaseObjectType == DatabaseObjectType.Trigger) dbObj = GetSelectedNode().Parent?.Tag as Table;

        displayInfo.Content = scriptTemplate.GetTemplateContent(databaseObjectType, scriptAction, dbObj);
        displayInfo.ScriptAction = scriptAction;

        ShowContent(displayInfo);
    }

    private void tsmiAlter_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.ALTER);
    }

    private void tsmiNewTable_Click(object sender, EventArgs e)
    {
        ShowContent(DatabaseObjectDisplayType.TableDesigner);
    }

    private void tsmiDesign_Click(object sender, EventArgs e)
    {
        ShowContent(DatabaseObjectDisplayType.TableDesigner, false);
    }

    private async void RefreshFolderNode()
    {
        var node = tvDbObjects.SelectedNode;

        if (node == null) return;

        if (node.Tag is DatabaseObject && node.Parent != null && CanRefresh(node.Parent))
        {
            var selectedName = node.Name;

            var parentNode = node.Parent;

            await LoadChildNodes(parentNode);

            foreach (TreeNode child in parentNode.Nodes)
                if (child.Name == selectedName)
                {
                    tvDbObjects.SelectedNode = child;
                    break;
                }

            tvDbObjects.SelectedNode = parentNode;
        }
        else if (!(node.Tag is DatabaseObject) && CanRefresh(node))
        {
            tvDbObjects.SelectedNode = node;

            await LoadChildNodes(node);
        }
    }

    private async void tsmiBackup_Click(object sender, EventArgs e)
    {
        var connectionInfo = GetCurrentConnectionInfo();

        var dbManager = new DbManager();

        dbManager.Subscribe(this);

        Action<BackupSetting> backup = setting =>
        {
            var success = dbManager.Backup(setting, connectionInfo);

            if (success) MessageBox.Show("Backup finished.");
        };

        var form = new frmBackupSettingRedefine { DatabaseType = DatabaseType };

        if (form.ShowDialog() == DialogResult.OK) await Task.Run(() => backup(form.Setting));
    }

    private void tsmiDiagnose_Click(object sender, EventArgs e)
    {
        var connectionInfo = GetCurrentConnectionInfo();

        var form = new frmDiagnose();
        form.DatabaseType = DatabaseType;
        form.ConnectionInfo = connectionInfo;

        if (DatabaseType == DatabaseType.Oracle) form.Schema = GetDatabaseNode(GetSelectedNode()).Name;

        form.Init(this);
        form.ShowDialog();
    }

    private void tsmiCopy_Click(object sender, EventArgs e)
    {
        var form = new frmTableCopy
        {
            DatabaseType = DatabaseType,
            ConnectionInfo = GetCurrentConnectionInfo(),
            Table = tvDbObjects.SelectedNode.Tag as Table
        };

        form.OnFeedback += Feedback;

        form.ShowDialog();
    }

    private void tsmiCompare_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        CompareDatabase(node);
    }

    private void CompareDatabase(TreeNode node)
    {
        var database = node.Tag as Database;

        var frmCompare = new frmCompare(DatabaseType, GetConnectionInfo(database.Name));
        frmCompare.ShowDialog();
    }

    private void tsmiCreateScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.CREATE);
    }

    private void tsmiSelectScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.SELECT);
    }

    private void tsmiInsertScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.INSERT);
    }

    private void tsmiUpdateScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.UPDATE);
    }

    private void tsmiDeleteScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.DELETE);
    }

    private void tsmiViewDependency_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        var tag = node.Tag;
        Database database = null;

        if (tag is Database tag1)
        {
            database = tag1;

            var tableDependency = new frmTableDependency(DatabaseType, GetConnectionInfo(database.Name), null);

            tableDependency.Show();
        }
        else if (tag is DatabaseObject dbObj)
        {
            database = GetDatabaseNode(node).Tag as Database;

            var dbOjectDependency = new frmDbObjectDependency(DatabaseType, GetConnectionInfo(database.Name), dbObj);

            dbOjectDependency.Show();
        }
    }

    private void tsmiCopyChildrenNames_Click(object sender, EventArgs e)
    {
        if (!IsValidSelectedNode()) return;

        var node = GetSelectedNode();

        if (node != null)
        {
            var dbObjects = node.Nodes.Cast<TreeNode>().Select(item => item.Tag as DatabaseObject);

            var isUniqueSchema = dbObjects.GroupBy(item => item.Schema).Count() == 1;

            var names = dbObjects.Select(item => isUniqueSchema ? item.Name : $"{item.Schema}.{item.Name}");

            var content = string.Join(Environment.NewLine, names);

            var frm = new frmTextContent(content);
            frm.Show();
        }
    }

    private void tsmiExecuteScript_Click(object sender, EventArgs e)
    {
        GenerateScripts(ScriptAction.EXECUTE);
    }

    private async void tsmiStatistic_Click(object sender, EventArgs e)
    {
        var connectionInfo = GetCurrentConnectionInfo();

        var statistic = new DbStatistic(DatabaseType, connectionInfo);

        statistic.OnFeedback += OnFeedback;

        var records = await statistic.CountTableRecords();

        var form = new frmTableRecordCount();

        form.LoadData(records);

        form.ShowDialog();
    }
}