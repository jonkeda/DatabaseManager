using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Helper;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Manager;
using Databases.Manager.Script;
using Databases.Model.Enum;

namespace DatabaseManager;

public partial class frmTranslateScript : Form
{
    private bool isHighlightingRequesting;
    private bool isPasting;

    public frmTranslateScript()
    {
        InitializeComponent();
    }

    private void frmTranslateScript_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        splitContainer1.SplitterDistance = (splitContainer1.Width - splitContainer1.SplitterWidth) / 2;

        LoadDbTypes();

        if (SettingManager.Setting.ValidateScriptsAfterTranslated) chkValidateScriptsAfterTranslated.Checked = true;
    }

    public void LoadDbTypes()
    {
        var databaseTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

        foreach (var value in databaseTypes)
        {
            cboSourceDbType.Items.Add(value.ToString());
            cboTargetDbType.Items.Add(value.ToString());
        }
    }

    private void btnTranlate_Click(object sender, EventArgs e)
    {
        StartTranslate();
    }

    private void StartTranslate()
    {
        if (ValidateInputs()) Task.Run(() => Translate());
    }

    private bool ValidateInputs()
    {
        var sourceDbTypeName = cboSourceDbType.Text;
        var targetDbTypeName = cboTargetDbType.Text;

        if (string.IsNullOrEmpty(sourceDbTypeName) || string.IsNullOrEmpty(targetDbTypeName))
        {
            MessageBox.Show("Please specify the source and target database type.");
            return false;
        }

/*        if (sourceDbTypeName == targetDbTypeName)
        {
            MessageBox.Show("The source database type can't be same as target.");
            return false;
        }*/

        var sourceScript = txtSource.Text.Trim();

        if (string.IsNullOrEmpty(sourceScript))
        {
            MessageBox.Show("The source script can't be empty.");
            return false;
        }

        return true;
    }

    private void Translate()
    {
        var sourceDbTypeName = cboSourceDbType.Text;
        var targetDbTypeName = cboTargetDbType.Text;
        var sourceScript = txtSource.Text.Trim();

        btnTranlate.Enabled = false;
        ClearSelection(txtSource);
        txtTarget.Clear();

        var sourceDbType = (DatabaseType)Enum.Parse(typeof(DatabaseType), sourceDbTypeName);
        var targetDbType = (DatabaseType)Enum.Parse(typeof(DatabaseType), targetDbTypeName);

        try
        {
            var translateManager = new TranslateManager();

            var result = translateManager.Translate(sourceDbType, targetDbType, sourceScript);

            var resultData = result.Data?.ToString();

            txtTarget.Text = resultData;

            if (result.HasError)
            {
                var msgBox = new frmTextContent("Error Message", result.Error.ToString(), true);
                msgBox.ShowDialog();

                RichTextBoxHelper.HighlightingError(txtSource, result.Error);

                return;
            }
            else if (string.IsNullOrEmpty(resultData) && sourceScript.Length > 0)
            {
                MessageBox.Show(
                    "The tanslate result is empty, please check whether the source database type is right.");
                return;
            }

            if (chkValidateScriptsAfterTranslated.Checked) ValidateScripts(targetDbType);

            HighlightingRichTextBox(txtTarget, cboTargetDbType);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
        }
        finally
        {
            btnTranlate.Enabled = true;
        }
    }

    private async void ValidateScripts(DatabaseType databaseType, bool showMessageBox = false)
    {
        var script = txtTarget.Text.Trim();

        if (string.IsNullOrEmpty(script)) return;

        var error = await Task.Run(() => ScriptValidator.ValidateSyntax(databaseType, script));

        if (error != null && error.HasError)
        {
            if (showMessageBox)
            {
                var msgBox = new frmTextContent("Error Message", error.ToString(), true);
                msgBox.ShowDialog();
            }

            RichTextBoxHelper.HighlightingError(txtTarget, error);
        }
        else
        {
            if (showMessageBox) MessageBox.Show("The scripts is valid.");
        }
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void HighlightingRichTextBox(RichTextBox richTextBox, ComboBox comboBox)
    {
        if (!string.IsNullOrEmpty(comboBox.Text))
        {
            var dbType = (DatabaseType)Enum.Parse(typeof(DatabaseType), comboBox.Text);

            if (chkHighlighting.Checked) RichTextBoxHelper.Highlighting(richTextBox, dbType, false, null, null, true);
        }
    }

    private void txtSource_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V) isPasting = true;
    }

    private void txtSource_KeyUp(object sender, KeyEventArgs e)
    {
        if (isPasting) HandlePaste();
    }

    private void HandlePaste()
    {
        txtSource.SelectAll();
        txtSource.SelectionColor = Color.Black;
    }

    private void txtSource_SelectionChanged(object sender, EventArgs e)
    {
        if (isPasting || isHighlightingRequesting)
        {
            isPasting = false;
            isHighlightingRequesting = false;

            HighlightingRichTextBox(txtSource, cboSourceDbType);
        }
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        txtSource.Text = "";
        txtTarget.Text = "";
    }

    private void txtSource_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            tsmiPaste.Visible = txtSource.Text.Trim().Length == 0 || txtSource.SelectionLength == txtSource.Text.Length;

            sourceContextMenuStrip.Show(txtSource, e.Location);
        }
    }

    private void tsmiPaste_Click(object sender, EventArgs e)
    {
        var data = Clipboard.GetDataObject();

        if (data != null)
        {
            HandlePaste();

            txtSource.Text = data.GetData(DataFormats.UnicodeText)?.ToString();

            isPasting = true;
            txtSource.SelectAll();
            txtSource.Select(0, 0);
        }
    }

    private void tsmiCopy_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(txtTarget.Text)) Clipboard.SetDataObject(txtTarget.Text);
    }

    private void txtTarget_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            tsmiCopy.Visible = txtTarget.Text.Length > 0;

            targetContextMenuStrip.Show(txtTarget, e.Location);
        }
    }

    private void btnExchange_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(cboSourceDbType.Text) && !string.IsNullOrEmpty(cboTargetDbType.Text))
        {
            var temp = cboSourceDbType.Text;
            cboSourceDbType.Text = cboTargetDbType.Text;
            cboTargetDbType.Text = temp;
        }
    }

    private void chkHighlighting_CheckedChanged(object sender, EventArgs e)
    {
        if (chkHighlighting.Checked)
        {
            HighlightingRichTextBox(txtSource, cboSourceDbType);
            HighlightingRichTextBox(txtTarget, cboTargetDbType);
        }
    }

    private void frmTranslateScript_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5) StartTranslate();
    }

    private void ClearSelection(RichTextBox txtEditor)
    {
        var start = txtEditor.SelectionStart;

        txtEditor.SelectAll();
        txtEditor.SelectionBackColor = Color.White;
        txtEditor.SelectionStart = start;
        txtEditor.SelectionLength = 0;
    }

    private void tsmiValidateScripts_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(cboTargetDbType.Text)) return;

        ClearSelection(txtTarget);

        var targetDbType = (DatabaseType)Enum.Parse(typeof(DatabaseType), cboTargetDbType.Text);

        ValidateScripts(targetDbType, true);
    }

    private void targetContextMenuStrip_Opening(object sender, CancelEventArgs e)
    {
        tsmiValidateScripts.Visible = txtTarget.Text.Trim().Length > 0;
    }

    private delegate void AddNodeDelegate();
}