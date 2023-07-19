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
using DatabaseManager.Helper;
using View = DatabaseInterpreter.Model.View;

namespace DatabaseManager.Forms;

public partial class frmDbObjectDependency : Form
{
    private readonly ConnectionInfo connectionInfo;
    private readonly DatabaseType databaseType;
    private readonly DbInterpreter dbInterpreter;
    private readonly DatabaseObject dbObject;
    private bool hasStyled;

    public frmDbObjectDependency()
    {
        InitializeComponent();
    }

    public frmDbObjectDependency(DatabaseType databaseType, ConnectionInfo connectionInfo, DatabaseObject dbObject)
    {
        InitializeComponent();

        this.databaseType = databaseType;
        this.connectionInfo = connectionInfo;
        this.dbObject = dbObject;

        dbInterpreter = DbInterpreterHelper.GetDbInterpreter(this.databaseType, this.connectionInfo,
            new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple });
    }

    private void frmDbObjectDependency_Load(object sender, EventArgs e)
    {
        InitControls();

        ShowDependencies();
    }

    private void InitControls()
    {
        rbDependOnThis.Text = RelaceNamePlaceHolder(rbDependOnThis.Text);
        rbThisDependOn.Text = RelaceNamePlaceHolder(rbThisDependOn.Text);
    }

    private string RelaceNamePlaceHolder(string text)
    {
        return text.Replace("$Name$", dbInterpreter.GetQuotedDbObjectNameWithSchema(dbObject));
    }

    private async void ShowDependencies()
    {
        if (tvDependencies.Nodes.Count == 1) tvDependencies.Nodes[0].Nodes.Clear();

        var fetcher = new DepencencyFetcher(dbInterpreter);

        var usages = await fetcher.Fetch(dbObject, rbDependOnThis.Checked);

        AddTreeNodes(dbObject, usages);
    }

    private void AddTreeNodes(DatabaseObject dbObject, List<DbObjectUsage> usages)
    {
        TreeNode rootNode = null;

        if (tvDependencies.Nodes.Count == 1)
        {
            rootNode = tvDependencies.Nodes[0];
        }
        else
        {
            rootNode = DbObjectsTreeHelper.CreateTreeNode(dbObject);
            rootNode.Tag = dbObject;

            tvDependencies.Nodes.Add(rootNode);
        }

        AddChildNodes(rootNode, usages);

        tvDependencies.SelectedNode = rootNode;
        rootNode.Expand();
    }

    private void AddChildNodes(TreeNode parentNode, List<DbObjectUsage> usages)
    {
        parentNode.Nodes.Clear();

        foreach (var usage in usages)
        {
            TreeNode node = null;

            if (rbDependOnThis.Checked)
                node = DbObjectsTreeHelper.CreateTreeNode(usage.ObjectName, usage.ObjectName, GetImageKey(usage));
            else
                node = DbObjectsTreeHelper.CreateTreeNode(usage.RefObjectSchema, usage.RefObjectName,
                    GetImageKey(usage));

            node.Tag = usage;

            var isSelfReference = IsSelfReferenceTable(dbObject, usage);

            if (!isSelfReference)
                if (rbDependOnThis.Checked)
                    node.Nodes.Add(DbObjectsTreeHelper.CreateFakeNode());

            parentNode.Nodes.Add(node);
        }
    }

    private string GetImageKey(DbObjectUsage usage)
    {
        var objectType = rbDependOnThis.Checked ? usage.ObjectType : usage.RefObjectType;

        return objectType;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void tvDependencies_AfterSelect(object sender, TreeViewEventArgs e)
    {
        txtObjectType.Text = "";
        txtObjectName.Text = "";

        var node = tvDependencies.SelectedNode;

        if (node == null || node.Tag == null) return;

        var tag = node.Tag;

        if (tag is DatabaseObject dbObj)
        {
            txtObjectType.Text = DbObjectHelper.GetDatabaseObjectType(dbObj).ToString();
            txtObjectName.Text = dbInterpreter.GetQuotedDbObjectNameWithSchema(dbObj);
        }
        else if (tag is DbObjectUsage usage)
        {
            if (rbDependOnThis.Checked)
            {
                txtObjectType.Text = usage.ObjectType;

                txtObjectName.Text = GetDbObjectFullName(usage.ObjectSchema, usage.ObjectName);
            }
            else if (rbThisDependOn.Checked)
            {
                txtObjectType.Text = usage.RefObjectType;

                txtObjectName.Text = GetDbObjectFullName(usage.RefObjectSchema, usage.RefObjectName);
            }
        }
    }

    private string GetDbObjectFullName(string schema, string name)
    {
        return dbInterpreter.GetQuotedDbObjectNameWithSchema(schema, name);
    }

    private void rbDependOnThis_CheckedChanged(object sender, EventArgs e)
    {
        ShowDependencies();
    }

    private bool IsSelfReferenceTable(DatabaseObject dbObject, DbObjectUsage usage)
    {
        if (dbObject is Table)
        {
            if (rbDependOnThis.Checked)
                return usage.ObjectSchema == dbObject.Schema && usage.ObjectName == dbObject.Name;
            return usage.RefObjectSchema == dbObject.Schema && usage.RefObjectName == dbObject.Name;
        }

        return false;
    }

    private void tvDependencies_BeforeExpand(object sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;

        if (!IsOnlyHasFakeChild(node)) return;

        tvDependencies.BeginInvoke(async () => await LoadChildNodes(node));
    }

    private async Task LoadChildNodes(TreeNode node)
    {
        ShowLoading(node);

        var tag = node.Tag;

        if (tag is DbObjectUsage usage)
        {
            var objectType = usage.ObjectType;

            var dboObj = new DatabaseObject { Schema = usage.ObjectSchema, Name = usage.ObjectName };

            switch (objectType)
            {
                case nameof(Table):
                    dboObj = ObjectHelper.CloneObject<Table>(dboObj);
                    break;
                case nameof(View):
                    dboObj = ObjectHelper.CloneObject<View>(dboObj);
                    break;
                case nameof(Function):
                    dboObj = ObjectHelper.CloneObject<Function>(dboObj);
                    break;
                case nameof(Procedure):
                    dboObj = ObjectHelper.CloneObject<Procedure>(dboObj);
                    break;
                default:
                    return;
            }

            var fetcher = new DepencencyFetcher(dbInterpreter);

            var usages = await fetcher.Fetch(dboObj, rbDependOnThis.Checked);

            AddChildNodes(node, usages);
        }
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

    private bool IsOnlyHasFakeChild(TreeNode node)
    {
        if (node.Nodes.Count == 1 && node.Nodes[0].Name == DbObjectsTreeHelper.FakeNodeName) return true;

        return false;
    }

    private void tvDependencies_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F) FindChildren();
    }

    private void FindChildren()
    {
        var findBox = new frmFindBox();

        var result = findBox.ShowDialog();

        if (result == DialogResult.OK)
        {
            var word = findBox.FindWord;

            ClearStyles(tvDependencies.Nodes);

            FindTreeNode(word, tvDependencies.Nodes);
        }
    }

    private void FindTreeNode(string word, TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            var text = node.Text.Split('.').LastOrDefault();

            if (text.ToLower() == word.ToLower())
            {
                node.BackColor = Color.LightBlue;
                hasStyled = true;

                if (node.Nodes.Count > 0) node.EnsureVisible();
            }
            else
            {
                FindTreeNode(word, node.Nodes);
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
}