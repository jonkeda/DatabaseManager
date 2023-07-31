using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Controls;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Interpreter.Utility.Helper;
using Databases.Interpreter.Utility.Model;
using Databases.Manager.Manager;
using Databases.Manager.Model.DbObjectDisplay;

namespace DatabaseManager;

public partial class frmMain : Form, IObserver<FeedbackInfo>
{
    public frmMain()
    {
        InitializeComponent();
    }

    public void OnNext(FeedbackInfo value)
    {
        Feedback(value);
    }

    public void OnError(Exception error)
    {
        throw new NotImplementedException();
    }

    public void OnCompleted()
    {
        throw new NotImplementedException();
    }

    private void frmMain_Load(object sender, EventArgs e)
    {
        InitControls();

        CheckForIllegalCrossThreadCalls = false;
        CheckForIllegalCrossThreadCalls = false;

        FeedbackHelper.EnableLog = SettingManager.Setting.EnableLog;
        LogHelper.LogType = SettingManager.Setting.LogType;
        FeedbackHelper.EnableDebug = true;
    }

    private void InitControls()
    {
        navigator.OnShowContent += ShowDbObjectContent;
        navigator.OnFeedback += Feedback;
        ucContent.OnDataFilter += DataFilter;
        ucContent.OnFeedback += Feedback;
    }

    private void ShowDbObjectContent(DatabaseObjectDisplayInfo content)
    {
        ucContent.ShowContent(content);
    }

    private void DataFilter(object sender)
    {
        var dataViewer = sender as UC_DataViewer;

        var filter = new frmDataFilter
            { Columns = dataViewer.Columns.ToList(), ConditionBuilder = dataViewer.ConditionBuilder };
        if (filter.ShowDialog() == DialogResult.OK) dataViewer.FilterData(filter.ConditionBuilder);
    }

    private void Feedback(FeedbackInfo info)
    {
        txtMessage.ForeColor = Color.Black;

        if (info.InfoType == FeedbackInfoType.Error)
        {
            if (!info.IgnoreError) MessageBox.Show(info.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            txtMessage.Text = info.Message;
            txtMessage.BackColor = BackColor;
            txtMessage.ForeColor = Color.Red;
        }
        else
        {
            txtMessage.Text = info.Message;
        }
    }

    private void tsmiSetting_Click(object sender, EventArgs e)
    {
        var frmSetting = new frmSetting();
        frmSetting.ShowDialog();
    }

    private void btnConvert_Click(object sender, EventArgs e)
    {
        var frmConvert = new frmConvert();
        frmConvert.ShowDialog();
    }

    private void frmMain_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
            if (FormEventCenter.OnSave != null)
                FormEventCenter.OnSave();
    }

    private void tsmiDbConnection_Click(object sender, EventArgs e)
    {
        var frmDbConnectionManage = new frmDbConnectionManage();
        frmDbConnectionManage.ShowDialog();
    }

    private void tsBtnGenerateScripts_Click(object sender, EventArgs e)
    {
        var frmGenerateScripts = new frmGenerateScripts();
        frmGenerateScripts.ShowDialog();
    }

    private void tsBtnConvert_Click(object sender, EventArgs e)
    {
        var frmConvert = new frmConvert();
        frmConvert.ShowDialog();
    }

    private void tsBtnAddQuery_Click(object sender, EventArgs e)
    {
        var connectionInfo = navigator.GetCurrentConnectionInfo();

        if (connectionInfo != null)
        {
            var info = new DatabaseObjectDisplayInfo
                { IsNew = true, DisplayType = DatabaseObjectDisplayType.Script, DatabaseType = navigator.DatabaseType };

            info.ConnectionInfo = connectionInfo;

            ShowDbObjectContent(info);
        }
        else
        {
            MessageBox.Show("Please select a database from left navigator first.");
        }
    }

    private void tsBtnRun_Click(object sender, EventArgs e)
    {
        RunScripts();
    }

    private void RunScripts()
    {
        ucContent.RunScripts();
    }

    private void tsBtnSave_Click(object sender, EventArgs e)
    {
        if (FormEventCenter.OnSave != null) FormEventCenter.OnSave();
    }

    private void tsBtnOpenFile_Click(object sender, EventArgs e)
    {
        if (dlgOpenFile == null) dlgOpenFile = new OpenFileDialog();

        if (dlgOpenFile.ShowDialog() == DialogResult.OK) LoadFile(dlgOpenFile.FileName);
    }

    private void LoadFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var info = navigator.GetDisplayInfo();

        info.DisplayType = DatabaseObjectDisplayType.Script;
        info.FilePath = filePath;
        info.Name = Path.GetFileName(info.FilePath);

        ucContent.ShowContent(info);
    }

    private void frmMain_DragOver(object sender, DragEventArgs e)
    {
        SetDragEffect(e);
    }

    private void frmMain_DragDrop(object sender, DragEventArgs e)
    {
        SetDropFiles(e);
    }

    private void SetDragEffect(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    private void SetDropFiles(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var filePath in filePaths) LoadFile(filePath);
        }
    }

    private void tsmiBackupSetting_Click(object sender, EventArgs e)
    {
        var frm = new frmBackupSetting();
        frm.ShowDialog();
    }

    private void tsBtnCompare_Click(object sender, EventArgs e)
    {
        var form = new frmCompare();
        form.ShowDialog();
    }

    private void tsmiLock_Click(object sender, EventArgs e)
    {
        var lockApp = new frmLockApp();
        lockApp.ShowDialog();
    }

    private void txtMessage_MouseHover(object sender, EventArgs e)
    {
        toolTip1.SetToolTip(txtMessage, txtMessage.Text);
    }

    private void tsBtnTranslateScript_Click(object sender, EventArgs e)
    {
        var translateScript = new frmTranslateScript();
        translateScript.Show();
    }

    private void tsmiWktView_Click(object sender, EventArgs e)
    {
        var geomViewer = new frmWktViewer();
        geomViewer.Show();
    }

    private void tsmiImageViewer_Click(object sender, EventArgs e)
    {
        var imgViewer = new frmImageViewer();
        imgViewer.Show();
    }
}