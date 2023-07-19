using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseConverter.Core;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Helper;

namespace DatabaseManager;

public partial class frmConvert : Form, IObserver<FeedbackInfo>
{
    private const string DONE = "Convert finished";
    private IEnumerable<CheckBox> configCheckboxes;
    private DbConverter dbConverter;
    private List<SchemaMappingInfo> schemaMappings = new();
    private readonly DatabaseType sourceDatabaseType;
    private ConnectionInfo sourceDbConnectionInfo;
    private ConnectionInfo targetDbConnectionInfo;
    private readonly bool useSourceConnector = true;

    public frmConvert()
    {
        InitializeComponent();
    }

    public frmConvert(DatabaseType sourceDatabaseType, ConnectionInfo sourceConnectionInfo)
    {
        InitializeComponent();

        this.sourceDatabaseType = sourceDatabaseType;
        sourceDbConnectionInfo = sourceConnectionInfo;
        useSourceConnector = false;
    }

    private void frmMain_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        CheckForIllegalCrossThreadCalls = false;
        CheckForIllegalCrossThreadCalls = false;

        if (!useSourceConnector)
        {
            var increaseHeight = sourceDbProfile.Height;
            sourceDbProfile.Visible = false;
            btnFetch.Height = targetDbProfile.ClientHeight;
            targetDbProfile.Top -= increaseHeight;
            tvDbObjects.Top -= increaseHeight;
            gbConfiguration.Top -= increaseHeight;
            tvDbObjects.Height += increaseHeight;
            gbConfiguration.Height += increaseHeight;

            if (sourceDatabaseType == DatabaseType.MySql || sourceDatabaseType == DatabaseType.Oracle)
            {
                btnSetSchemaMappings.Enabled = false;
                chkCreateSchemaIfNotExists.Enabled = false;
                chkCreateSchemaIfNotExists.Checked = false;
            }
        }

        cboMode.SelectedIndex = 0;

        configCheckboxes = gbConfiguration.Controls.Cast<Control>()
            .Where(item => item is CheckBox).Cast<CheckBox>().ToArray();

        SetControlStateByMode();
    }

    private void btnFetch_Click(object sender, EventArgs e)
    {
        Invoke(Fetch);
    }

    private async void Fetch()
    {
        DatabaseType dbType;

        if (useSourceConnector)
        {
            dbType = sourceDbProfile.DatabaseType;

            if (!sourceDbProfile.IsDbTypeSelected())
            {
                MessageBox.Show("Please select a source database type.");
                return;
            }

            if (!sourceDbProfile.IsProfileSelected())
            {
                MessageBox.Show("Please select a source database profile.");
                return;
            }

            if (!sourceDbProfile.ValidateProfile()) return;
        }
        else
        {
            dbType = sourceDatabaseType;
        }

        btnFetch.Text = "...";

        try
        {
            await tvDbObjects.LoadTree(dbType, sourceDbConnectionInfo);
            btnExecute.Enabled = true;
        }
        catch (Exception ex)
        {
            tvDbObjects.ClearNodes();

            var message = ExceptionHelper.GetExceptionDetails(ex);

            LogHelper.LogError(message);

            MessageBox.Show("Error:" + message);
        }

        btnFetch.Text = "Fetch";
    }

    private async void btnExecute_Click(object sender, EventArgs e)
    {
        txtMessage.ForeColor = Color.Black;
        txtMessage.Text = "";

        await Task.Run(() => Convert());
    }

    private bool ValidateSource(SchemaInfo schemaInfo)
    {
        if (!tvDbObjects.HasDbObjectNodeSelected())
        {
            MessageBox.Show("Please select objects from tree.");
            return false;
        }

        if (sourceDbConnectionInfo == null)
        {
            MessageBox.Show("Source connection is null.");
            return false;
        }

        return true;
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

    private GenerateScriptMode GetGenerateScriptMode()
    {
        var scriptMode = GenerateScriptMode.None;

        if (cboMode.SelectedIndex == 0 || cboMode.SelectedIndex == 2)
            scriptMode = scriptMode | GenerateScriptMode.Schema;

        if (cboMode.SelectedIndex == 1 || cboMode.SelectedIndex == 2) scriptMode = scriptMode | GenerateScriptMode.Data;

        return scriptMode;
    }

    private async Task Convert()
    {
        var schemaInfo = tvDbObjects.GetSchemaInfo();

        if (!ValidateSource(schemaInfo)) return;

        if (targetDbConnectionInfo == null)
        {
            MessageBox.Show("Target connection info is null.");
            return;
        }

        if (!targetDbProfile.ValidateProfile()) return;

        if (sourceDbConnectionInfo.Server == targetDbConnectionInfo.Server
            && sourceDbConnectionInfo.Port == targetDbConnectionInfo.Port
            && sourceDbConnectionInfo.Database == targetDbConnectionInfo.Database)
        {
            MessageBox.Show("Source database cannot be equal to the target database.");
            return;
        }

        var sourceDbType = useSourceConnector ? sourceDbProfile.DatabaseType : sourceDatabaseType;
        var targetDbType = targetDbProfile.DatabaseType;

        var sourceScriptOption = new DbInterpreterOption
        {
            ScriptOutputMode = GenerateScriptOutputMode.None,
            SortObjectsByReference = true,
            GetTableAllObjects = true,
            ThrowExceptionWhenErrorOccurs = true,
            ExcludeGeometryForData = chkExcludeGeometryForData.Checked
        };

        var targetScriptOption = new DbInterpreterOption
        {
            ScriptOutputMode = GenerateScriptOutputMode.WriteToString,
            ThrowExceptionWhenErrorOccurs = true,
            ExcludeGeometryForData = chkExcludeGeometryForData.Checked
        };

        SetGenerateScriptOption(sourceScriptOption, targetScriptOption);

        if (chkGenerateSourceScripts.Checked)
            sourceScriptOption.ScriptOutputMode =
                sourceScriptOption.ScriptOutputMode | GenerateScriptOutputMode.WriteToFile;

        if (chkOutputScripts.Checked)
            targetScriptOption.ScriptOutputMode =
                targetScriptOption.ScriptOutputMode | GenerateScriptOutputMode.WriteToFile;

        if (chkTreatBytesAsNull.Checked)
        {
            sourceScriptOption.TreatBytesAsNullForReading = true;
            targetScriptOption.TreatBytesAsNullForExecuting = true;
        }

        targetScriptOption.TableScriptsGenerateOption.GenerateIdentity = chkGenerateIdentity.Checked;
        targetScriptOption.TableScriptsGenerateOption.GenerateComment = chkGenerateComment.Checked;

        var scriptMode = GetGenerateScriptMode();

        targetScriptOption.ScriptMode = scriptMode;

        if (scriptMode == GenerateScriptMode.None)
        {
            MessageBox.Show("Please specify the script mode.");
            return;
        }

        var source = new DbConveterInfo
        {
            DbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(sourceDbType, sourceDbConnectionInfo, sourceScriptOption)
        };
        var target = new DbConveterInfo
        {
            DbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(targetDbType, targetDbConnectionInfo, targetScriptOption)
        };

        try
        {
            using (dbConverter = new DbConverter(source, target))
            {
                var option = dbConverter.Option;

                option.GenerateScriptMode = scriptMode;
                option.BulkCopy = chkBulkCopy.Checked;
                option.ExecuteScriptOnTargetServer = chkExecuteOnTarget.Checked;
                option.UseTransaction = chkUseTransaction.Checked;
                option.ContinueWhenErrorOccurs = chkContinueWhenErrorOccurs.Checked;
                option.ConvertComputeColumnExpression = chkComputeColumn.Checked;
                option.OnlyCommentComputeColumnExpressionInScript = chkOnlyCommentComputeExpression.Checked;
                option.SplitScriptsToExecute = true;
                option.UseOriginalDataTypeIfUdtHasOnlyOneAttr =
                    SettingManager.Setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr;
                option.CreateSchemaIfNotExists = chkCreateSchemaIfNotExists.Checked;
                option.NcharToDoubleChar = chkNcharToDoubleChar.Checked;
                option.ConvertConcatChar =
                    TranslateHelper.NeedConvertConcatChar(SettingManager.Setting.ConvertConcatCharTargetDatabases,
                        targetDbType);
                option.CollectTranslateResultAfterTranslated = false;

                option.SchemaMappings = schemaMappings;

                if (sourceDbType == DatabaseType.MySql) source.DbInterpreter.Option.InQueryItemLimitCount = 2000;

                if (targetDbType == DatabaseType.MySql) target.DbInterpreter.Option.RemoveEmoji = true;

                dbConverter.Subscribe(this);

                SetExecuteButtonEnabled(false);

                var result = await dbConverter.Convert(schemaInfo);

                SetExecuteButtonEnabled(true);

                if (result.InfoType == DbConvertResultInfoType.Information)
                {
                    if (!dbConverter.CancelRequested)
                    {
                        txtMessage.AppendText(Environment.NewLine + DONE);
                        MessageBox.Show(result.Message, "Information", MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Task has been canceled.");
                    }
                }
                else if (result.InfoType == DbConvertResultInfoType.Warnning)
                {
                    MessageBox.Show(result.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (result.InfoType == DbConvertResultInfoType.Error)
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        catch (Exception ex)
        {
            if (dbConverter != null) dbConverter = null;

            HandleException(ex);
        }
        finally
        {
            GC.Collect();
        }
    }

    private void SetExecuteButtonEnabled(bool enable)
    {
        btnExecute.Enabled = enable;
        btnCancel.Enabled = !enable;
    }

    private void HandleException(Exception ex)
    {
        var errMsg = ExceptionHelper.GetExceptionDetails(ex);

        LogHelper.LogError(errMsg);

        AppendMessage(errMsg, true);

        txtMessage.SelectionStart = txtMessage.TextLength;
        txtMessage.ScrollToCaret();

        btnExecute.Enabled = true;
        btnCancel.Enabled = false;

        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void Feedback(FeedbackInfo info)
    {
        Invoke(() =>
        {
            if (info.InfoType == FeedbackInfoType.Error)
            {
                if (!info.IgnoreError)
                    if (chkExecuteOnTarget.Checked && !chkContinueWhenErrorOccurs.Checked)
                    {
                        if (dbConverter != null && dbConverter.IsBusy) dbConverter.Cancle();

                        SetExecuteButtonEnabled(true);
                    }

                AppendMessage(info.Message, true);
            }
            else
            {
                AppendMessage(info.Message);
            }
        });
    }

    private void AppendMessage(string message, bool isError = false)
    {
        RichTextBoxHelper.AppendMessage(txtMessage, message, isError);
    }

    private bool ConfirmCancel()
    {
        if (MessageBox.Show("Are you sure to abandon current task?", "Confirm", MessageBoxButtons.YesNo) ==
            DialogResult.Yes) return true;
        return false;
    }

    private async void btnCancel_Click(object sender, EventArgs e)
    {
        await Task.Run(() =>
        {
            if (dbConverter != null && dbConverter.IsBusy)
                if (ConfirmCancel())
                {
                    dbConverter?.Cancle();

                    SetExecuteButtonEnabled(true);
                }
        });
    }

    private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (dbConverter != null && dbConverter.IsBusy)
        {
            if (ConfirmCancel())
            {
                dbConverter.Cancle();
                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
            }
        }
    }

    private void btnCopyMessage_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(txtMessage.Text))
        {
            Clipboard.SetDataObject(txtMessage.Text);
            MessageBox.Show("The message has been copied to clipboard.");
        }
        else
        {
            MessageBox.Show("There's no message.");
        }
    }

    private void btnSaveMessage_Click(object sender, EventArgs e)
    {
        if (dlgSaveLog == null) dlgSaveLog = new SaveFileDialog();

        if (!string.IsNullOrEmpty(txtMessage.Text))
        {
            dlgSaveLog.Filter = "txt files|*.txt|all files|*.*";
            var dialogResult = dlgSaveLog.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                File.WriteAllLines(dlgSaveLog.FileName,
                    txtMessage.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                dlgSaveLog.Reset();
            }
        }
        else
        {
            MessageBox.Show("There's no message.");
        }
    }

    private void btnOutputFolder_Click(object sender, EventArgs e)
    {
        if (dlgOutputFolder == null) dlgOutputFolder = new FolderBrowserDialog();

        var result = dlgOutputFolder.ShowDialog();

        if (result == DialogResult.OK) txtOutputFolder.Text = dlgOutputFolder.SelectedPath;
    }

    private void sourceDbProfile_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        sourceDbConnectionInfo = connectionInfo;

        if (useSourceConnector) SetControlsStatus();
    }

    private void targetDbProfile_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        targetDbConnectionInfo = connectionInfo;

        SetControlsStatus();
    }

    private void SetControlsStatus()
    {
        var targetProfile = targetDbProfile;

        var enable = false;

        if (targetProfile.IsDbTypeSelected())
        {
            var databaseType = targetProfile.DatabaseType;

            enable = !(databaseType == DatabaseType.Oracle || databaseType == DatabaseType.MySql
                                                           || sourceDatabaseType == DatabaseType.Oracle ||
                                                           sourceDatabaseType == DatabaseType.MySql);

            var targetDbInterpreter =
                DbInterpreterHelper.GetDbInterpreter(databaseType, targetDbConnectionInfo, new DbInterpreterOption());

            chkNcharToDoubleChar.Enabled = !targetDbInterpreter.SupportNchar;
        }
        else
        {
            chkNcharToDoubleChar.Enabled = false;
        }

        btnSetSchemaMappings.Enabled = enable;

        if (!enable)
        {
            chkCreateSchemaIfNotExists.Enabled = false;
            chkCreateSchemaIfNotExists.Checked = false;
        }
        else
        {
            chkCreateSchemaIfNotExists.Enabled = true;
        }

        if (!chkNcharToDoubleChar.Enabled) chkNcharToDoubleChar.Checked = true;

        schemaMappings = new List<SchemaMappingInfo>();
    }

    private void txtMessage_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
            if (txtMessage.SelectionLength > 0)
                contextMenuStrip1.Show(txtMessage, e.Location);
    }

    private void tsmiCopySelection_Click(object sender, EventArgs e)
    {
        Clipboard.SetDataObject(txtMessage.SelectedText);
    }

    private void chkComputeColumn_CheckedChanged(object sender, EventArgs e)
    {
        chkOnlyCommentComputeExpression.Enabled = !chkComputeColumn.Checked;

        if (chkComputeColumn.Checked) chkOnlyCommentComputeExpression.Checked = false;
    }

    private async void btnSetSchemaMappings_Click(object sender, EventArgs e)
    {
        var sourceDbType = useSourceConnector ? sourceDbProfile.DatabaseType : sourceDatabaseType;
        var targetDbType = targetDbProfile.DatabaseType;

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown) return;

        if (sourceDbConnectionInfo == null || targetDbConnectionInfo == null) return;

        var option = new DbInterpreterOption();

        var sourceInterpreter = DbInterpreterHelper.GetDbInterpreter(sourceDbType, sourceDbConnectionInfo, option);
        var targetInterpreter = DbInterpreterHelper.GetDbInterpreter(targetDbType, targetDbConnectionInfo, option);

        var sourceSchemas = new List<DatabaseSchema>();
        var targetSchemas = new List<DatabaseSchema>();

        try
        {
            sourceSchemas = await sourceInterpreter.GetDatabaseSchemasAsync();
            targetSchemas = await targetInterpreter.GetDatabaseSchemasAsync();
        }
        catch (Exception ex)
        {
        }

        var form = new frmSchemaMapping
        {
            Mappings = schemaMappings,
            SourceSchemas = sourceSchemas.Select(item => item.Name).ToList(),
            TargetSchemas = targetSchemas.Select(item => item.Name).ToList()
        };

        if (form.ShowDialog() == DialogResult.OK) schemaMappings = form.Mappings;
    }

    private void chkOutputScripts_CheckedChanged(object sender, EventArgs e)
    {
        var defaultOutputFolder = SettingManager.Setting.ScriptsDefaultOutputFolder;

        if (chkOutputScripts.Checked && string.IsNullOrEmpty(txtOutputFolder.Text) &&
            !string.IsNullOrEmpty(defaultOutputFolder)) txtOutputFolder.Text = defaultOutputFolder;
    }

    private void cboMode_SelectedIndexChanged(object sender, EventArgs e)
    {
        SetControlStateByMode();
    }

    private void SetControlStateByMode()
    {
        if (configCheckboxes == null) return;

        var mode = GetGenerateScriptMode();

        var schemaOnly = mode == GenerateScriptMode.Schema;
        var dataOnly = mode == GenerateScriptMode.Data;

        var schemaOnlyCheckboxes = configCheckboxes.Where(item => item.Tag?.ToString() == "Schema");
        var dataOnlyCheckboxes = configCheckboxes.Where(item => item.Tag?.ToString() == "Data");

        foreach (var checkbox in schemaOnlyCheckboxes) checkbox.Enabled = !dataOnly;

        foreach (var checkbox in dataOnlyCheckboxes) checkbox.Enabled = !schemaOnly;

        if (schemaOnly || dataOnly)
        {
            UncheckIfNotEnable();
        }
        else
        {
            chkCreateSchemaIfNotExists.Checked = true;

            chkGenerateIdentity.Checked = true;
            chkGenerateComment.Checked = true;
            chkComputeColumn.Checked = true;
        }

        chkBulkCopy.Checked = !schemaOnly;
        chkUseTransaction.Checked = true;

        SetControlsStatus();
    }

    private void UncheckIfNotEnable()
    {
        foreach (var checkbox in configCheckboxes)
            if (!checkbox.Enabled)
                checkbox.Checked = false;
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