using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using Databases.Interpreter.Helper;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.Model.Schema;

namespace DatabaseManager;

public partial class frmTableDependency : Form
{
    private readonly ConnectionInfo connectionInfo;
    private readonly DatabaseType databaseType;
    private readonly DatabaseObject dbObject;
    private bool hasStyled;
    private IEnumerable<TreeNode> notReferencedTreeNodes;

    public frmTableDependency()
    {
        InitializeComponent();
    }

    public frmTableDependency(DatabaseType databaseType, ConnectionInfo connectionInfo, DatabaseObject dbObject)
    {
        InitializeComponent();
        this.databaseType = databaseType;
        this.connectionInfo = connectionInfo;
        this.dbObject = dbObject;
    }

    private void frmDependency_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        txtName.Focus();
        LoadTree();
    }

    private async void LoadTree()
    {
        tvDbObjects.Nodes.Clear();

        var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

        var databaseObjectType = DatabaseObjectType.Table | DatabaseObjectType.ForeignKey;

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(databaseType, connectionInfo, option);

        var filter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };

        var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

        IEnumerable<Table> tables = schemaInfo.Tables;
        var foreignKeys = schemaInfo.TableForeignKeys;

        var isUniqueDbSchema = tables.GroupBy(item => item.Schema).Count() == 1;

        var notReferencedTables =
            schemaInfo.Tables.Where(item => !foreignKeys.Any(fk => fk.ReferencedTableName == item.Name));

        notReferencedTreeNodes = DbObjectsTreeHelper.CreateDbObjectNodes(notReferencedTables, !isUniqueDbSchema);

        var topReferencedTableNames = TableReferenceHelper.GetTopReferencedTableNames(foreignKeys);

        var topReferencedTables = tables.Where(item => topReferencedTableNames.Any(t => t == item.Name));

        var children = DbObjectsTreeHelper.CreateDbObjectNodes(topReferencedTables, !isUniqueDbSchema).ToArray();

        tvDbObjects.Nodes.AddRange(children);

        LoadChildNodes(tvDbObjects.Nodes.Cast<TreeNode>(), isUniqueDbSchema, tables, foreignKeys);

        if (dbObject != null)
        {
            txtName.Text = dbObject.Name;

            LocateNode(dbObject.Name);
        }
    }

    private void LoadChildNodes(IEnumerable<TreeNode> nodes, bool isUniqueSchema, IEnumerable<Table> tables,
        IEnumerable<TableForeignKey> foreignKeys)
    {
        foreach (var tn in nodes)
        {
            var table = tn.Tag as Table;

            var refTableNames = foreignKeys.Where(item => item.ReferencedTableName == table.Name)
                .Select(item => item.TableName);

            var refTables = tables.Where(item => refTableNames.Any(t => t == item.Name));

            var children = DbObjectsTreeHelper.CreateDbObjectNodes(refTables, !isUniqueSchema).ToArray();

            tn.Nodes.AddRange(children);

            LoadChildNodes(children.Where(item => item.Text != tn.Text), isUniqueSchema, tables, foreignKeys);
        }
    }

    private void btnLocate_Click(object sender, EventArgs e)
    {
        var name = txtName.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please input a name to locate.");
            return;
        }

        LocateNode(name);
    }

    private void LocateNode(string name)
    {
        if (hasStyled) ClearStyles(tvDbObjects.Nodes);

        LocateNode(name, tvDbObjects.Nodes);
    }

    private void LocateNode(string name, TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            var table = node.Tag as Table;

            if (table.Name.ToLower() == name.ToLower())
            {
                node.BackColor = Color.LightBlue;
                hasStyled = true;

                if (node.Nodes.Count > 0) node.EnsureVisible();
            }
            else
            {
                LocateNode(name, node.Nodes);
            }
        }
    }

    private void ClearStyles(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            node.BackColor = Color.White;

            ClearStyles(node.Nodes);
        }

        hasStyled = false;
    }

    private void SetMenuItemVisible()
    {
        tsmiExpandChildren.Visible = tvDbObjects.SelectedNode != null;
        tsmiClearStyles.Visible = hasStyled;
    }

    private void tsmiExpandAll_Click(object sender, EventArgs e)
    {
        tvDbObjects.ExpandAll();

        var node = tvDbObjects.SelectedNode;

        node?.EnsureVisible();
    }

    private void tsmiCollapseAll_Click(object sender, EventArgs e)
    {
        tvDbObjects.CollapseAll();
    }

    private void chkShowNotReferenced_CheckedChanged(object sender, EventArgs e)
    {
        if (notReferencedTreeNodes != null)
        {
            var show = chkShowNotReferenced.Checked;

            var nodes = tvDbObjects.Nodes.Cast<TreeNode>();

            if (show)
            {
                var index = 0;

                foreach (var node in notReferencedTreeNodes)
                    if (!nodes.Any(item => item.Text == node.Text))
                    {
                        tvDbObjects.Nodes.Insert(index, node);

                        index++;
                    }
            }
            else
            {
                var indexes = new List<int>();

                var index = 0;

                foreach (var node in notReferencedTreeNodes)
                {
                    if (nodes.Any(item => item.Text == node.Text)) indexes.Add(index);

                    index++;
                }

                indexes.Reverse();

                indexes.ForEach(item => tvDbObjects.Nodes.RemoveAt(item));
            }
        }
    }

    private void txtName_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            var name = txtName.Text.Trim();

            if (!string.IsNullOrEmpty(name)) LocateNode(name);
        }
    }

    private void tsmiExpandChildren_Click(object sender, EventArgs e)
    {
        var selectedNode = tvDbObjects.SelectedNode;

        selectedNode?.ExpandAll();
    }

    private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
    {
        SetMenuItemVisible();
    }

    private void tvDbObjects_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var isIn = e.Node.Bounds.Contains(new Point(e.X, e.Y));

            if (isIn) tvDbObjects.SelectedNode = e.Node;
        }
    }

    private void tsmiClearStyles_Click(object sender, EventArgs e)
    {
        ClearStyles(tvDbObjects.Nodes);
    }
}