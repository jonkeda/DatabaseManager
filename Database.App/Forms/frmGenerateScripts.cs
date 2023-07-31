using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Helper;
using Databases.Interpreter;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Interpreter.Utility.Model;
using Databases.Manager.Manager;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;
using Databases.Model.Schema;
using Databases.ScriptGenerator;

namespace DatabaseManager;

public partial class frmGenerateScripts : Form, IObserver<FeedbackInfo>
{
    private ConnectionInfo connectionInfo;
    private readonly DatabaseType databaseType;
    private DbInterpreter dbInterpreter;
    private bool isBusy;
    private readonly bool useConnector = true;

    public frmGenerateScripts()
    {
        InitializeComponent();
    }

    public frmGenerateScripts(DatabaseType databaseType, ConnectionInfo connectionInfo)
    {
        InitializeComponent();
        this.databaseType = databaseType;
        this.connectionInfo = connectionInfo;

        useConnector = false;
    }

    private void frmGenerateScripts_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        if (!useConnector && connectionInfo != null)
        {
            panelConnector.Visible = false;
            tvDbObjects.Top -= panelConnector.Height;
            tvDbObjects.Height += panelConnector.Height;

            Connect();
        }

        var defaultOutputFolder = SettingManager.Setting.ScriptsDefaultOutputFolder;

        if (!string.IsNullOrEmpty(defaultOutputFolder)) txtOutputFolder.Text = defaultOutputFolder;
    }

    private void btnOutputFolder_Click(object sender, EventArgs e)
    {
        if (dlgOutputFolder == null) dlgOutputFolder = new FolderBrowserDialog();

        var result = dlgOutputFolder.ShowDialog();
        if (result == DialogResult.OK) txtOutputFolder.Text = dlgOutputFolder.SelectedPath;
    }

    private void btnConnect_Click(object sender, EventArgs e)
    {
        Invoke(Connect);
    }

    private async void Connect()
    {
        DatabaseType dbType;

        if (useConnector)
        {
            if (!dbConnectionProfile.IsDbTypeSelected())
            {
                MessageBox.Show("Please select a database type.");
                return;
            }

            if (!dbConnectionProfile.IsProfileSelected())
            {
                MessageBox.Show("Please select a database profile.");
                return;
            }

            if (!dbConnectionProfile.ValidateProfile()) return;

            dbType = dbConnectionProfile.DatabaseType;
        }
        else
        {
            dbType = databaseType;
        }

        btnConnect.Text = "...";

        try
        {
            await tvDbObjects.LoadTree(dbType, connectionInfo);
        }
        catch (Exception ex)
        {
            tvDbObjects.ClearNodes();

            var message = ExceptionHelper.GetExceptionDetails(ex);

            LogHelper.LogError(message);

            MessageBox.Show("Error:" + message);
        }

        btnConnect.Text = "Connect";
    }

    private void dbConnectionProfile_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        this.connectionInfo = connectionInfo;
    }

    private async void btnGenerate_Click(object sender, EventArgs e)
    {
        await Task.Run(() => GenerateScripts());
    }

    private async void GenerateScripts()
    {
        var schemaInfo = tvDbObjects.GetSchemaInfo();

        if (!Validate(schemaInfo)) return;

        isBusy = true;
        btnGenerate.Enabled = false;

        var dbType = useConnector ? dbConnectionProfile.DatabaseType : databaseType;

        var option = new DbInterpreterOption
        {
            ScriptOutputMode = GenerateScriptOutputMode.WriteToFile,
            SortObjectsByReference = true,
            GetTableAllObjects = true
        };

        if (chkTreatBytesAsNull.Checked)
        {
            option.TreatBytesAsNullForReading = true;
            option.TreatBytesAsNullForExecuting = true;
        }
        else
        {
            if (dbType == DatabaseType.Oracle)
            {
                option.TreatBytesAsNullForReading = true;
                option.TreatBytesAsNullForExecuting = true;
            }
            else
            {
                option.TreatBytesAsHexStringForFile = true;
            }
        }

        SetGenerateScriptOption(option);

        option.TableScriptsGenerateOption.GenerateIdentity = chkGenerateIdentity.Checked;
        option.TableScriptsGenerateOption.GenerateComment = chkGenerateComment.Checked;

        var scriptMode = GetGenerateScriptMode();

        if (scriptMode == GenerateScriptMode.None)
        {
            MessageBox.Show("Please specify the script mode.");
            return;
        }

        dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);

        var filter = new SchemaInfoFilter();

        SchemaInfoHelper.SetSchemaInfoFilterValues(filter, schemaInfo);

        try
        {
            schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

            dbInterpreter.Subscribe(this);

            var mode = GenerateScriptMode.None;

            var dbScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(dbInterpreter);

            if (scriptMode.HasFlag(GenerateScriptMode.Schema))
            {
                mode = GenerateScriptMode.Schema;
                dbScriptGenerator.GenerateSchemaScripts(schemaInfo);
            }

            if (scriptMode.HasFlag(GenerateScriptMode.Data))
            {
                mode = GenerateScriptMode.Data;
                await dbScriptGenerator.GenerateDataScriptsAsync(schemaInfo);
            }

            isBusy = false;

            var filePath = Path.GetFullPath(dbScriptGenerator.GetScriptOutputFilePath(mode));
            var tip = string.IsNullOrEmpty(txtOutputFolder.Text)
                ? $", the file path is:{Environment.NewLine}{filePath}"
                : "";

            MessageBox.Show($"Scripts have been generated{tip}", "Information", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }

        btnGenerate.Enabled = true;
    }

    private void HandleException(Exception ex)
    {
        isBusy = false;

        var errMsg = ExceptionHelper.GetExceptionDetails(ex);

        LogHelper.LogInfo(errMsg);

        AppendMessage(errMsg, true);

        txtMessage.SelectionStart = txtMessage.TextLength;
        txtMessage.ScrollToCaret();

        btnGenerate.Enabled = true;

        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private bool Validate(SchemaInfo schemaInfo)
    {
        if (!tvDbObjects.HasDbObjectNodeSelected())
        {
            MessageBox.Show("Please select objects from tree.");
            return false;
        }

        if (connectionInfo == null)
        {
            MessageBox.Show("Connection is null.");
            return false;
        }

        return true;
    }

    private GenerateScriptMode GetGenerateScriptMode()
    {
        var scriptMode = GenerateScriptMode.None;
        if (chkScriptSchema.Checked) scriptMode = scriptMode | GenerateScriptMode.Schema;
        if (chkScriptData.Checked) scriptMode = scriptMode | GenerateScriptMode.Data;

        return scriptMode;
    }

    private void SetGenerateScriptOption(params DbInterpreterOption[] options)
    {
        if (options != null)
        {
            var outputFolder = txtOutputFolder.Text.Trim();

            foreach (var option in options)
                if (Directory.Exists(outputFolder))
                    option.ScriptOutputFolder = outputFolder;
        }
    }

    private void Feedback(FeedbackInfo info)
    {
        Invoke(() =>
        {
            if (info.InfoType == FeedbackInfoType.Error)
                AppendMessage(info.Message, true);
            else
                AppendMessage(info.Message);
        });
    }

    private void AppendMessage(string message, bool isError = false)
    {
        RichTextBoxHelper.AppendMessage(txtMessage, message, isError);
    }

    private void frmGenerateScripts_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (isBusy)
        {
            if (ConfirmCancel())
                e.Cancel = false;
            else
                e.Cancel = true;
        }
    }

    private bool ConfirmCancel()
    {
        if (MessageBox.Show("Are you sure to abandon current task?", "Confirm", MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            if (dbInterpreter != null) dbInterpreter.CancelRequested = true;

            return true;
        }

        return false;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        if (isBusy)
            if (!ConfirmCancel())
                return;

        Close();
    }

    #region IObserver<FeedbackInfo>

    void IObserver<FeedbackInfo>.OnCompleted()
    {
    }

    void IObserver<FeedbackInfo>.OnError(Exception error)
    {
    }

    void IObserver<FeedbackInfo>.OnNext(FeedbackInfo info)
    {
        Feedback(info);
    }

    #endregion
}