using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Data;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Controls;

public partial class UC_SqlQuery : UserControl, IDbObjContentDisplayer, IObserver<FeedbackInfo>
{
    private DatabaseObjectDisplayInfo displayInfo;
    private string originalText = "";
    private bool readOnly;
    private ScriptRunner scriptRunner;
    private bool showEditorMessage = true;

    public UC_SqlQuery()
    {
        InitializeComponent();

        SetResultPanelVisible(false);
    }


    public bool ReadOnly
    {
        get => readOnly;
        set
        {
            readOnly = value;
            Editor.ReadOnly = value;
            Editor.BackColor = Color.White;
        }
    }

    public int SplitterDistance
    {
        get => splitContainer1.SplitterDistance;
        set => splitContainer1.SplitterDistance = value;
    }

    public bool ShowEditorMessage
    {
        get => showEditorMessage;
        set
        {
            showEditorMessage = value;
            statusStrip1.Visible = value;
        }
    }

    public RichTextBox Editor => queryEditor.Editor;

    public void Show(DatabaseObjectDisplayInfo displayInfo)
    {
        this.displayInfo = displayInfo;

        if (Editor.Text.Length > 0) Editor.ResetText();

        if (!string.IsNullOrEmpty(displayInfo.Content))
            Editor.AppendText(displayInfo.Content);
        else if (File.Exists(displayInfo.FilePath)) Editor.AppendText(File.ReadAllText(displayInfo.FilePath));

        queryEditor.DatabaseType = this.displayInfo.DatabaseType;

        queryEditor.Init();

        if (displayInfo.ConnectionInfo != null && !string.IsNullOrEmpty(displayInfo.ConnectionInfo.Database))
        {
            var dbInterpreter = GetDbInterpreter();

            if (dbInterpreter.BuiltinDatabases.Any(item =>
                    item.ToUpper() == this.displayInfo.ConnectionInfo.Database.ToUpper())) return;

            if (SettingManager.Setting.EnableEditorIntellisence) SetupIntellisence();
        }

        originalText = Editor.Text;
    }

    public ContentSaveResult Save(ContentSaveInfo info)
    {
        File.WriteAllText(info.FilePath, Editor.Text);

        return new ContentSaveResult { IsOK = true };
    }

    private void queryEditor_Load(object sender, EventArgs e)
    {
        if (showEditorMessage)
            queryEditor.OnQueryEditorInfoMessage += ShowEditorInfoMessage;
        else
            splitContainer1.Height += statusStrip1.Height;

        if (!readOnly) queryEditor.SetupIntellisenseRequired += QueryEditor_SetupIntellisenseRequired;
    }

    private void QueryEditor_SetupIntellisenseRequired(object sender, EventArgs e)
    {
        SetupIntellisence();
    }

    private void ShowEditorInfoMessage(string message)
    {
        tsslMessage.Text = message;
    }

    private DbInterpreter GetDbInterpreter()
    {
        var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

        return DbInterpreterHelper.GetDbInterpreter(displayInfo.DatabaseType, displayInfo.ConnectionInfo, option);
    }

    private async void SetupIntellisence()
    {
        if (CheckConnection())
        {
            var dbInterpreter = GetDbInterpreter();

            queryEditor.DbInterpreter = dbInterpreter;

            var filter = new SchemaInfoFilter
            {
                DatabaseObjectType = DatabaseObjectType.Table
                                     | DatabaseObjectType.Function
                                     | DatabaseObjectType.View
                                     | DatabaseObjectType.Column
            };

            var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

            DataStore.SetSchemaInfo(displayInfo.DatabaseType, schemaInfo);

            queryEditor.SetupIntellisence();
        }
    }

    public void ShowResult(QueryResult result)
    {
        if (result == null) return;

        if (result.DoNothing)
        {
            tabResult.SelectedIndex = 1;

            AppendMessage("Nothing can be done.");
        }
        else if (result.HasError)
        {
            tabResult.SelectedIndex = 1;

            AppendMessage(result.Result?.ToString(), true);
        }
        else
        {
            var selectedTabIndex = -1;

            if (result.ResultType == QueryResultType.Grid)
            {
                var dataTable = result.Result as DataTable;

                if (dataTable != null)
                {
                    selectedTabIndex = 0;

                    resultGridView.LoadData(dataTable);
                }
            }
            else if (result.ResultType == QueryResultType.Text)
            {
                selectedTabIndex = 1;

                if (resultTextBox.Text.Length == 0)
                    if (!(displayInfo.ScriptAction == ScriptAction.CREATE ||
                          displayInfo.ScriptAction == ScriptAction.ALTER))
                        if (result.Result is int affectedRows)
                            if (affectedRows >= 0)
                                AppendMessage($"{affectedRows} row(s) affected.");

                AppendMessage("command executed.");
            }

            if (selectedTabIndex >= 0) tabResult.SelectedIndex = selectedTabIndex;
        }

        SetResultPanelVisible(true);
    }

    public async void RunScripts(DatabaseObjectDisplayInfo data)
    {
        displayInfo = data;

        var script = Editor.SelectionLength > 0 ? Editor.SelectedText : Editor.Text;

        if (script.Trim().Length == 0) return;

        ClearResults();

        scriptRunner = new ScriptRunner();
        scriptRunner.Subscribe(this);

        if (CheckConnection())
        {
            var result = await scriptRunner.Run(data.DatabaseType, data.ConnectionInfo, script, data.ScriptAction,
                data.ScriptParameters);

            ShowResult(result);
        }
    }

    private bool CheckConnection()
    {
        if (displayInfo.ConnectionInfo == null)
        {
            var dbConnect = new frmDbConnect(displayInfo.DatabaseType) { NotUseProfile = true };

            if (dbConnect.ShowDialog() == DialogResult.OK)
            {
                displayInfo.ConnectionInfo = dbConnect.ConnectionInfo;

                if (SettingManager.Setting.EnableEditorIntellisence) SetupIntellisence();
            }
        }

        if (displayInfo.ConnectionInfo != null && !string.IsNullOrEmpty(displayInfo.ConnectionInfo.Database))
            return true;
        return false;
    }

    private void Feedback(FeedbackInfo info)
    {
        Invoke(() =>
        {
            if (info.InfoType == FeedbackInfoType.Error)
            {
                if (!info.IgnoreError)
                    if (scriptRunner != null && scriptRunner.IsBusy)
                        scriptRunner.Cancel();

                AppendMessage(info.Message, true);
            }
            else
            {
                AppendMessage(info.Message);
            }
        });
    }

    private void ClearResults()
    {
        resultTextBox.Clear();
        resultGridView.ClearData();
    }

    private void AppendMessage(string message, bool isError = false)
    {
        RichTextBoxHelper.AppendMessage(resultTextBox, message, isError, false);

        SetResultPanelVisible(true);
    }

    private void SetResultPanelVisible(bool visible)
    {
        splitContainer1.Panel2Collapsed = !visible;
        splitContainer1.SplitterWidth = visible ? 3 : 1;
    }

    internal bool IsTextChanged()
    {
        return originalText != Editor.Text;
    }

    internal void ValidateScripts()
    {
        queryEditor.ValidateScripts();
    }

    internal void DisposeResources()
    {
        queryEditor?.DisposeResources();
    }

    #region IObserver<FeedbackInfo>

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

    #endregion
}