using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Profile;
using Databases;

//using DatabaseManager.Controls.Model;

namespace DatabaseManager;

public partial class frmSetting : Form
{
    private List<string> convertConcatCharTargetDatabases;

    public frmSetting()
    {
        InitializeComponent();
    }

    private void frmSetting_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private async void InitControls()
    {
        tabControl1.SelectedIndex = 0;

        var dbObjectNameModes = Enum.GetNames(typeof(DbObjectNameMode));
        cboDbObjectNameMode.Items.AddRange(dbObjectNameModes);

        var setting = SettingManager.Setting;

        numCommandTimeout.Value = setting.CommandTimeout;
        numDataBatchSize.Value = setting.DataBatchSize;
        chkShowBuiltinDatabase.Checked = setting.ShowBuiltinDatabase;
        chkUseOriginalDataTypeIfUdtHasOnlyOneAttr.Checked = setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr;
        txtMySqlCharset.Text = setting.MySqlCharset;
        txtMySqlCharsetCollation.Text = setting.MySqlCharsetCollation;
        chkNotCreateIfExists.Checked = setting.NotCreateIfExists;
        chkEnableLog.Checked = setting.EnableLog;
        cboDbObjectNameMode.Text = setting.DbObjectNameMode.ToString();
        chkLogInfo.Checked = setting.LogType.HasFlag(LogType.Info);
        chkLogError.Checked = setting.LogType.HasFlag(LogType.Error);
        chkEnableEditorHighlighting.Checked = setting.EnableEditorHighlighting;
        chkEditorEnableIntellisence.Checked = setting.EnableEditorIntellisence;
        chkExcludePostgresExtensionObjects.Checked = setting.ExcludePostgresExtensionObjects;
        chkValidateScriptsAfterTranslated.Checked = setting.ValidateScriptsAfterTranslated;

        var dbTypes = Enum.GetNames(typeof(DatabaseType));
        cboPreferredDatabase.Items.AddRange(dbTypes);
        chkRememberPasswordDuringSession.Checked = setting.RememberPasswordDuringSession;
        cboPreferredDatabase.Text = setting.PreferredDatabase.ToString();
        txtOutputFolder.Text = setting.ScriptsDefaultOutputFolder;

        var ps = await PersonalSettingManager.GetPersonalSetting();

        if (ps != null && !string.IsNullOrEmpty(ps.LockPassword)) txtLockPassword.Text = ps.LockPassword;

        convertConcatCharTargetDatabases = setting.ConvertConcatCharTargetDatabases;
    }

    private async void btnConfirm_Click(object sender, EventArgs e)
    {
        var setting = SettingManager.Setting;
        setting.CommandTimeout = (int)numCommandTimeout.Value;
        setting.DataBatchSize = (int)numDataBatchSize.Value;
        setting.ShowBuiltinDatabase = chkShowBuiltinDatabase.Checked;
        setting.UseOriginalDataTypeIfUdtHasOnlyOneAttr = chkUseOriginalDataTypeIfUdtHasOnlyOneAttr.Checked;
        setting.MySqlCharset = txtMySqlCharset.Text.Trim();
        setting.MySqlCharsetCollation = txtMySqlCharsetCollation.Text.Trim();
        setting.NotCreateIfExists = chkNotCreateIfExists.Checked;
        setting.EnableLog = chkEnableLog.Checked;
        setting.DbObjectNameMode = (DbObjectNameMode)Enum.Parse(typeof(DbObjectNameMode), cboDbObjectNameMode.Text);
        setting.RememberPasswordDuringSession = chkRememberPasswordDuringSession.Checked;
        setting.EnableEditorHighlighting = chkEnableEditorHighlighting.Checked;
        setting.EnableEditorIntellisence = chkEditorEnableIntellisence.Checked;
        setting.ExcludePostgresExtensionObjects = chkExcludePostgresExtensionObjects.Checked;
        setting.ScriptsDefaultOutputFolder = txtOutputFolder.Text;
        setting.ValidateScriptsAfterTranslated = chkValidateScriptsAfterTranslated.Checked;

        var password = txtLockPassword.Text.Trim();

        var ps = new PersonalSetting { LockPassword = password };

        await PersonalSettingManager.Save(ps);

        if (cboPreferredDatabase.SelectedIndex >= 0)
            setting.PreferredDatabase = (DatabaseType)Enum.Parse(typeof(DatabaseType), cboPreferredDatabase.Text);

        var logType = LogType.None;

        if (chkLogInfo.Checked) logType |= LogType.Info;

        if (chkLogError.Checked) logType |= LogType.Error;

        setting.LogType = logType;

        setting.ConvertConcatCharTargetDatabases = convertConcatCharTargetDatabases;

        SettingManager.SaveConfig(setting);

        DbInterpreter.Setting = SettingManager.GetInterpreterSetting();
    }

    private void btnOutputFolder_Click(object sender, EventArgs e)
    {
        if (dlgOutputFolder == null) dlgOutputFolder = new FolderBrowserDialog();

        var result = dlgOutputFolder.ShowDialog();

        if (result == DialogResult.OK) txtOutputFolder.Text = dlgOutputFolder.SelectedPath;
    }

    private void btnSelectTargetDatabaseTypesForConcatChar_Click(object sender, EventArgs e)
    {
        var selector = new frmItemsSelector("Select Database Types",
            ItemsSelectorHelper.GetDatabaseTypeItems(convertConcatCharTargetDatabases));

        if (selector.ShowDialog() == DialogResult.OK)
            convertConcatCharTargetDatabases = selector.CheckedItem.Select(item => item.Name).ToList();
    }
}