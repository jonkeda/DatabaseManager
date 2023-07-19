using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using View = DatabaseInterpreter.Model.View;

namespace DatabaseManager.Controls;

public partial class UC_DbObjectsSimpleTree : UserControl
{
    public UC_DbObjectsSimpleTree()
    {
        InitializeComponent();
        CheckForIllegalCrossThreadCalls = false;
    }

    public async Task LoadTree(DatabaseType dbType, ConnectionInfo connectionInfo)
    {
        tvDbObjects.Nodes.Clear();

        var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

        var databaseObjectType = DbObjectsTreeHelper.DefaultObjectType;

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
        var filter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };

        var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(UserDefinedType), "User Defined Types",
            schemaInfo.UserDefinedTypes);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(Sequence), "Sequences", schemaInfo.Sequences);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(Function), "Functions", schemaInfo.Functions);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(Table), "Tables", schemaInfo.Tables);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(View), "Views", schemaInfo.Views);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(Procedure), "Procedures", schemaInfo.Procedures);
        tvDbObjects.Nodes.AddDbObjectFolderNode(nameof(TableTrigger), "Triggers", schemaInfo.TableTriggers);

        if (tvDbObjects.Nodes.Count == 1) tvDbObjects.ExpandAll();
    }

    public void ClearNodes()
    {
        tvDbObjects.Nodes.Clear();
    }

    private void tvDbObjects_AfterCheck(object sender, TreeViewEventArgs e)
    {
        if (e.Node.Nodes.Count > 0)
            foreach (TreeNode node in e.Node.Nodes)
                node.Checked = e.Node.Checked;
    }

    public SchemaInfo GetSchemaInfo()
    {
        var schemaInfo = new SchemaInfo();
        foreach (TreeNode node in tvDbObjects.Nodes)
        foreach (TreeNode item in node.Nodes)
            if (item.Checked)
                switch (node.Name)
                {
                    case nameof(UserDefinedType):
                        schemaInfo.UserDefinedTypes.Add(item.Tag as UserDefinedType);
                        break;
                    case nameof(Sequence):
                        schemaInfo.Sequences.Add(item.Tag as Sequence);
                        break;
                    case nameof(Table):
                        schemaInfo.Tables.Add(item.Tag as Table);
                        break;
                    case nameof(View):
                        schemaInfo.Views.Add(item.Tag as View);
                        break;
                    case nameof(Function):
                        schemaInfo.Functions.Add(item.Tag as Function);
                        break;
                    case nameof(Procedure):
                        schemaInfo.Procedures.Add(item.Tag as Procedure);
                        break;
                    case nameof(TableTrigger):
                        schemaInfo.TableTriggers.Add(item.Tag as TableTrigger);
                        break;
                }

        return schemaInfo;
    }

    public bool HasDbObjectNodeSelected()
    {
        foreach (TreeNode node in tvDbObjects.Nodes)
        foreach (TreeNode child in node.Nodes)
            if (child.Checked)
                return true;
        return false;
    }

    private void tsmiShowSortedNames_Click(object sender, EventArgs e)
    {
        var node = contextMenuStrip1.Tag as TreeNode;

        if (node != null)
        {
            var dbObjects = node.Nodes.Cast<TreeNode>().Select(item => item.Tag as DatabaseObject)
                .OrderBy(item => item.Order);

            var isUniqueSchema = dbObjects.GroupBy(item => item.Schema).Count() == 1;

            var names = dbObjects.Select(item => isUniqueSchema ? item.Name : $"{item.Schema}.{item.Name}");

            var content = string.Join(Environment.NewLine, names);

            var frm = new frmTextContent(content);
            frm.Show();
        }
    }

    private void tvDbObjects_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var isIn = e.Node.Bounds.Contains(new Point(e.X, e.Y));

            if (isIn)
                if (e.Node.Parent == null)
                {
                    contextMenuStrip1.Show(Cursor.Position);

                    tvDbObjects.SelectedNode = e.Node;
                    contextMenuStrip1.Tag = e.Node;
                }
        }
    }

    private void tvDbObjects_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control)
            if (e.KeyCode == Keys.F)
                if (tvDbObjects.SelectedNode != null)
                    FindChild();
    }

    private void FindChild()
    {
        var node = tvDbObjects.SelectedNode;

        var findBox = new frmFindBox();

        var result = findBox.ShowDialog();

        if (result == DialogResult.OK)
        {
            var word = findBox.FindWord;

            var nodes = node.Nodes.Count == 0 ? node.Parent.Nodes : node.Nodes;

            var foundNode = FindTreeNode(nodes, word);

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

    private TreeNode FindTreeNode(TreeNodeCollection nodes, string word)
    {
        foreach (TreeNode node in nodes)
        {
            var tag = node.Tag;

            if (node.Tag != null)
            {
                var text = node.Text.Split('.').LastOrDefault()?.Trim();

                if (text.ToUpper() == word.ToUpper()) return node;
            }
        }

        return null;
    }
}