using System;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Model;

namespace DatabaseManager;

public partial class frmDiagnose : Form
{
    private DbManager dbManager;

    public frmDiagnose()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public ConnectionInfo ConnectionInfo { get; set; }
    public string Schema { get; set; }

    private void frmDiagnose_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        if (DatabaseType == DatabaseType.Oracle)
        {
            rbNotNullWithEmpty.Enabled = false;
            rbSelfReferenceSame.Checked = true;
        }

        if (DatabaseType == DatabaseType.Oracle || DatabaseType == DatabaseType.Postgres ||
            DatabaseType == DatabaseType.Sqlite) tabControl.TabPages.Remove(tabForScript);
    }

    public void Init(IObserver<FeedbackInfo> observer)
    {
        dbManager = new DbManager();

        dbManager.Subscribe(observer);
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        var tabPageName = tabControl.SelectedTab.Name;

        if (tabPageName == tabForTable.Name)
            DiagnoseTable();
        else if (tabPageName == tabForScript.Name) DiagnoseScript();
    }

    private async void DiagnoseTable()
    {
        var diagnoseType = TableDiagnoseType.None;

        if (rbNotNullWithEmpty.Checked)
            diagnoseType = TableDiagnoseType.NotNullWithEmpty;
        else if (rbWithLeadingOrTrailingWhitespace.Checked)
            diagnoseType = TableDiagnoseType.WithLeadingOrTrailingWhitespace;
        else if (rbSelfReferenceSame.Checked) diagnoseType = TableDiagnoseType.SelfReferenceSame;

        if (diagnoseType == TableDiagnoseType.None)
        {
            MessageBox.Show("Please select a type for table diagnose.");
            return;
        }

        try
        {
            btnStart.Enabled = false;

            var result = await dbManager.DiagnoseTable(DatabaseType, ConnectionInfo, Schema, diagnoseType);

            if (result.Details.Count > 0)
            {
                var frmResult = new frmTableDiagnoseResult
                {
                    DatabaseType = DatabaseType,
                    ConnectionInfo = ConnectionInfo
                };

                frmResult.LoadResult(result);
                frmResult.ShowDialog();
            }
            else
            {
                MessageBox.Show("Diagnosis finished, no invalid data found.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex), "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnStart.Enabled = true;
        }
    }

    private async void DiagnoseScript()
    {
        var diagnoseType = ScriptDiagnoseType.None;

        if (rbViewColumnAliasWithoutQuotationChar.Checked)
            diagnoseType = ScriptDiagnoseType.ViewColumnAliasWithoutQuotationChar;
        else if (rbNameNotMatchForScript.Checked) diagnoseType = ScriptDiagnoseType.NameNotMatch;

        if (diagnoseType == ScriptDiagnoseType.None)
        {
            MessageBox.Show("Please select a type for script diagnose.");
            return;
        }

        try
        {
            btnStart.Enabled = false;

            var results = await dbManager.DiagnoseScript(DatabaseType, ConnectionInfo, Schema, diagnoseType);

            if (results.Count > 0)
            {
                var frmResult = new frmScriptDiagnoseResult
                {
                    DatabaseType = DatabaseType,
                    ConnectionInfo = ConnectionInfo,
                    DiagnoseType = diagnoseType
                };

                frmResult.LoadResults(results);
                frmResult.ShowDialog();
            }
            else
            {
                MessageBox.Show("Diagnosis finished, no invalid data found.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex), "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnStart.Enabled = true;
        }
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}