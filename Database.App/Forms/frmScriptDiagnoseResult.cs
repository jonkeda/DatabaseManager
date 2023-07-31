using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.Diagnose;
using Databases.Manager.Script;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Enum;
using Databases.Model.Option;
using View = Databases.Model.DatabaseObject.View;

namespace DatabaseManager.Forms;

public partial class frmScriptDiagnoseResult : Form
{
    private bool isRemovingTreeNode;
    private List<ScriptDiagnoseResult> results;

    public frmScriptDiagnoseResult()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public ConnectionInfo ConnectionInfo { get; set; }
    public ScriptDiagnoseType DiagnoseType { get; set; }

    private void frmScriptDiagnoseResult_Load(object sender, EventArgs e)
    {
    }

    public void LoadResults(List<ScriptDiagnoseResult> results)
    {
        this.results = results;

        LoadTree(results);
    }

    private void LoadTree(List<ScriptDiagnoseResult> results)
    {
        var views = results.Where(item => item.DbObject is View).Select(item => item.DbObject as View);
        var functions = results.Where(item => item.DbObject is Function).Select(item => item.DbObject as Function);
        var procedures = results.Where(item => item.DbObject is Procedure).Select(item => item.DbObject as Procedure);

        if (views.Count() > 0) AddTreeNodes("Views", views);

        if (functions.Count() > 0) AddTreeNodes("Functions", functions);

        if (procedures.Count() > 0) AddTreeNodes("Procedures", procedures);

        if (tvDbObjects.Nodes.Count == 1)
        {
            tvDbObjects.ExpandAll();

            if (tvDbObjects.Nodes[0].Nodes.Count == 1) tvDbObjects.SelectedNode = tvDbObjects.Nodes[0].Nodes[0];
        }
    }

    private void AddTreeNodes(string folderName, IEnumerable<ScriptDbObject> dbObjects)
    {
        var viewFolderNode = DbObjectsTreeHelper.CreateFolderNode(folderName, folderName);

        tvDbObjects.Nodes.Add(viewFolderNode);

        viewFolderNode.AddDbObjectNodes(dbObjects);
    }

    private void tvDbObjects_AfterSelect(object sender, TreeViewEventArgs e)
    {
        if (isRemovingTreeNode) return;

        var node = tvDbObjects.SelectedNode;

        if (node != null && node.Tag is ScriptDbObject dbObject) ShowResultDetails(dbObject);
    }

    private void ShowResultDetails(ScriptDbObject dbObject)
    {
        var result = results.FirstOrDefault(item => item.DbObject == dbObject);

        dgvResultDetails.Rows.Clear();

        if (result != null)
        {
            var details = result.Details;

            foreach (var detail in details.OrderBy(item => item.Index))
            {
                var rowIndex = dgvResultDetails.Rows.Add();

                var row = dgvResultDetails.Rows[rowIndex];

                row.Cells[colObjectType.Name].Value = detail.ObjectType.ToString();
                row.Cells[colName.Name].Value = detail.Name;
                row.Cells[colInvalidName.Name].Value = detail.InvalidName;

                row.Tag = detail;
            }

            dgvResultDetails.ClearSelection();

            txtDefinition.Text = dbObject.Definition;

            RichTextBoxHelper.HighlightingWord(txtDefinition,
                details.Select(item => new WordMatchInfo { Index = item.Index, Length = item.InvalidName.Length }),
                Color.Yellow);
        }
    }

    private void dgvResult_SelectionChanged(object sender, EventArgs e)
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvResultDetails);

        if (row != null && row.Tag != null && txtDefinition.Text.Length > 0)
        {
            var detail = row.Tag as ScriptDiagnoseResultDetail;

            txtDefinition.SelectionStart = detail.Index;
            txtDefinition.SelectionLength = detail.InvalidName.Length;

            txtDefinition.ScrollToCaret();
        }
    }

    private void dgvResultDetails_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        dgvResultDetails.ClearSelection();
    }

    private async void btnCorrect_Click(object sender, EventArgs e)
    {
        var node = tvDbObjects.SelectedNode;

        if (node == null)
        {
            MessageBox.Show("Please select a tree node.");
            return;
        }

        if (node.Tag == null)
        {
            MessageBox.Show("Please select a valid tree node.");
            return;
        }

        btnCorrect.Enabled = false;

        var dbObject = node.Tag as ScriptDbObject;

        var result = results.FirstOrDefault(item => item.DbObject == dbObject);

        if (result != null) await CorrectScripts(new[] { result });

        btnCorrect.Enabled = true;
    }

    private async void btnCorrectAll_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Are you sure to correct all of the scripts?", "Confirm", MessageBoxButtons.YesNo);

        if (result == DialogResult.Yes)
        {
            btnCorrectAll.Enabled = false;

            await CorrectScripts(results);

            btnCorrectAll.Enabled = true;
        }
    }

    private async Task CorrectScripts(IEnumerable<ScriptDiagnoseResult> results)
    {
        try
        {
            var dbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(DatabaseType, ConnectionInfo, new DbInterpreterOption());

            var scriptCorrector = new ScriptCorrector(dbInterpreter);

            if (DiagnoseType == ScriptDiagnoseType.NameNotMatch ||
                DiagnoseType == ScriptDiagnoseType.ViewColumnAliasWithoutQuotationChar)
                results = await scriptCorrector.CorrectNotMatchNames(DiagnoseType, results);

            MessageBox.Show("Script has been corrected.");

            RemoveNodesAfterCorrected(results);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
        }
        finally
        {
            isRemovingTreeNode = false;
            btnCorrect.Enabled = true;
            btnCorrectAll.Enabled = true;
        }
    }

    private void RemoveNodesAfterCorrected(IEnumerable<ScriptDiagnoseResult> results)
    {
        var nodes = GetDbObjectTreeNodes();
        var selectedNode = tvDbObjects.SelectedNode;

        var count = 0;

        isRemovingTreeNode = true;

        foreach (var node in nodes)
        {
            var dbObject = node.Tag as ScriptDbObject;
            var result = results.FirstOrDefault(item => item.DbObject == dbObject);

            if (result != null)
            {
                if (node == selectedNode)
                {
                    txtDefinition.Clear();
                    dgvResultDetails.Rows.Clear();
                }

                node.Parent.Nodes.Remove(node);

                count++;

                var res = this.results.FirstOrDefault(item => item.DbObject == dbObject);

                if (res != null) this.results.Remove(res);
            }
        }

        if (count == results.Count())
        {
            var node = tvDbObjects.SelectedNode;

            if (node == null || node.Tag is null || this.results.Count == 0) txtDefinition.Clear();
        }

        isRemovingTreeNode = false;
    }

    private List<TreeNode> GetDbObjectTreeNodes()
    {
        var nodes = new List<TreeNode>();

        var folderNodes = tvDbObjects.Nodes;

        foreach (TreeNode node in folderNodes) nodes.AddRange(node.Nodes.Cast<TreeNode>());

        return nodes;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}