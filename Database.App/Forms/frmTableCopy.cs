using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseConverter.Core;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;

namespace DatabaseManager;

public partial class frmTableCopy : Form, IObserver<FeedbackInfo>
{
    private DbConverter dbConverter;

    public FeedbackHandler OnFeedback;
    private ConnectionInfo targetDbConnectionInfo;

    public frmTableCopy()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public ConnectionInfo ConnectionInfo { get; set; }
    public Table Table { get; set; }

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

    private void frmDbObjectCopy_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        txtName.Text = Table.Name + "_copy";

        SetSchemaControlStates();
    }

    private async void btnExecute_Click(object sender, EventArgs e)
    {
        await CopyTable();
    }

    private async Task CopyTable()
    {
        var name = txtName.Text.Trim();

        if (!ValidateInputs()) return;

        try
        {
            var scriptMode = GetGenerateScriptMode();

            var isTableExisted = false;
            var isDifferentTableName = false;

            Action checkTableName = async () =>
            {
                if (scriptMode.HasFlag(GenerateScriptMode.Schema)) isTableExisted = await IsNameExisted(name);

                if (isTableExisted)
                {
                    name = name + "_copy";
                    isDifferentTableName = true;
                }
            };

            do
            {
                checkTableName();
            } while (isTableExisted);

            var schemaInfo = new SchemaInfo();
            schemaInfo.Tables.Add(Table);

            var targetDatabaseType = rbAnotherDatabase.Checked ? ucConnection.DatabaseType : DatabaseType;
            var targetConnectionInfo = rbAnotherDatabase.Checked ? targetDbConnectionInfo : ConnectionInfo;

            var sourceOption = new DbInterpreterOption { ThrowExceptionWhenErrorOccurs = true };
            var targetOption = new DbInterpreterOption { ThrowExceptionWhenErrorOccurs = true };

            targetOption.TableScriptsGenerateOption.GenerateIdentity = chkGenerateIdentity.Checked;

            var source = new DbConverterInfo
                { DbInterpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType, ConnectionInfo, sourceOption) };
            var target = new DbConverterInfo
            {
                DbInterpreter =
                    DbInterpreterHelper.GetDbInterpreter(targetDatabaseType, targetConnectionInfo, targetOption)
            };

            if (chkOnlyCopyTable.Checked)
                source.DatabaseObjectType = DatabaseObjectType.Table | DatabaseObjectType.Column;

            source.TableNameMappings.Add(Table.Name, name);

            btnExecute.Enabled = false;

            using (dbConverter = new DbConverter(source, target))
            {
                var option = dbConverter.Option;

                option.RenameTableChildren = isTableExisted || rbSameDatabase.Checked;
                option.GenerateScriptMode = scriptMode;
                option.BulkCopy = true;
                option.UseTransaction = true;
                option.ConvertComputeColumnExpression = true;
                option.IgnoreNotSelfForeignKey = true;
                option.UseOriginalDataTypeIfUdtHasOnlyOneAttr =
                    SettingManager.Setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr;
                option.OnlyForTableCopy = true;

                if (cboSchema.Visible)
                {
                    var targetSchema = string.IsNullOrEmpty(cboSchema.Text)
                        ? target.DbInterpreter.DefaultSchema
                        : cboSchema.Text;

                    dbConverter.Option.SchemaMappings.Add(new SchemaMappingInfo
                        { SourceSchema = Table.Schema, TargetSchema = targetSchema });
                }

                dbConverter.Subscribe(this);

                if (DatabaseType == DatabaseType.MySql) source.DbInterpreter.Option.InQueryItemLimitCount = 2000;

                dbConverter.Option.SplitScriptsToExecute = true;

                var result = await dbConverter.Convert(schemaInfo, Table.Schema);

                if (result.InfoType == DbConvertResultInfoType.Information)
                {
                    if (!dbConverter.CancelRequested)
                    {
                        var msg = "Table copied." + (isDifferentTableName
                            ? $@"{Environment.NewLine}The target table name is ""{name}""."
                            : "");

                        MessageBox.Show(msg, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Task has been canceled.");
                    }
                }
                else if (result.InfoType == DbConvertResultInfoType.Warning)
                {
                    MessageBox.Show(result.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if
                    (result.InfoType ==
                     DbConvertResultInfoType.Error) //message shows in main form because it uses Subscribe above
                {
                    // MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            btnExecute.Enabled = true;
        }
    }

    private GenerateScriptMode GetGenerateScriptMode()
    {
        var scriptMode = GenerateScriptMode.None;

        if (chkScriptSchema.Checked) scriptMode = scriptMode | GenerateScriptMode.Schema;
        if (chkScriptData.Checked) scriptMode = scriptMode | GenerateScriptMode.Data;

        return scriptMode;
    }

    private void HandleException(Exception ex)
    {
        var errMsg = ExceptionHelper.GetExceptionDetails(ex);

        LogHelper.LogError(errMsg);

        MessageBox.Show(errMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void rbSameDatabase_CheckedChanged(object sender, EventArgs e)
    {
        SetControlState();
    }

    private void SetControlState()
    {
        ucConnection.Enabled = rbAnotherDatabase.Checked;
        txtName.Text = rbSameDatabase.Checked ? $"{Table.Name}_copy" : Table.Name;

        SetSchemaControlStates();
    }

    private void ucConnection_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        targetDbConnectionInfo = connectionInfo;

        SetSchemaControlStates();
    }

    private void SetSchemaControlStates()
    {
        cboSchema.Text = "";
        cboSchema.Items.Clear();

        var targetDbInterpreter = GetTargetDbInterpreter();

        if (targetDbInterpreter != null)
        {
            var targetDbType = targetDbInterpreter.DatabaseType;

            lblSchema.Visible = cboSchema.Visible =
                targetDbType == DatabaseType.SqlServer || targetDbType == DatabaseType.Postgres;

            ShowSchemas();
        }
    }

    private async void ShowSchemas()
    {
        if (cboSchema.Visible)
        {
            if (rbAnotherDatabase.Checked && !ucConnection.ValidateProfile()) return;

            try
            {
                var targetDbSchemas = await GetTargetDbInterpreter().GetDatabaseSchemasAsync();

                foreach (var schema in targetDbSchemas)
                {
                    cboSchema.Items.Add(schema.Name);

                    if (Table.Schema == schema.Name) cboSchema.Text = schema.Name;
                }
            }
            catch (Exception ex)
            {
            }
        }
    }

    private bool ValidateInputs()
    {
        var name = txtName.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Name can't be empty.");
            return false;
        }

        if (rbAnotherDatabase.Checked)
            if (targetDbConnectionInfo == null)
            {
                MessageBox.Show("Please specify target database connection.");
                return false;
            }

        var scriptMode = GetGenerateScriptMode();

        if (scriptMode == GenerateScriptMode.None)
        {
            MessageBox.Show("Please specify the script mode.");
            return false;
        }

        return true;
    }

    private DbInterpreter GetTargetDbInterpreter()
    {
        var databaseType = rbAnotherDatabase.Checked ? ucConnection.DatabaseType : DatabaseType;

        var connectionInfo = rbAnotherDatabase.Checked ? targetDbConnectionInfo : ConnectionInfo;

        var option = new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(databaseType, connectionInfo, option);

        return dbInterpreter;
    }

    private async Task<bool> IsNameExisted(string name)
    {
        var dbInterpreter = GetTargetDbInterpreter();

        var filter = new SchemaInfoFilter { TableNames = new[] { name } };

        if (!string.IsNullOrEmpty(cboSchema.Text)) filter.Schema = cboSchema.Text;

        var tables = await dbInterpreter.GetTablesAsync(filter);

        if (tables.Count > 0) return true;

        return false;
    }

    private void Feedback(FeedbackInfo info)
    {
        OnFeedback?.Invoke(info);
    }
}