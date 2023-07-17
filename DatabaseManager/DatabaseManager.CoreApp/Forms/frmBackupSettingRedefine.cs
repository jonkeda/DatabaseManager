using System;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using DatabaseManager.Model;

namespace DatabaseManager;

public partial class frmBackupSettingRedefine : Form
{
    public frmBackupSettingRedefine()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public BackupSetting Setting { get; private set; }

    private void frmBackupSettingRedefine_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        Setting = BackupSettingManager.GetSettings()
            .FirstOrDefault(item => item.DatabaseType == DatabaseType.ToString());

        if (Setting != null)
        {
            txtSaveFolder.Text = Setting.SaveFolder;
            chkZipFile.Checked = Setting.ZipFile;
        }

        if (DatabaseType == DatabaseType.SqlServer || DatabaseType == DatabaseType.Postgres) chkZipFile.Enabled = false;
    }

    private void btnSaveFolder_Click(object sender, EventArgs e)
    {
        if (folderBrowserDialog1 == null) folderBrowserDialog1 = new FolderBrowserDialog();

        if (!string.IsNullOrEmpty(txtSaveFolder.Text)) folderBrowserDialog1.SelectedPath = txtSaveFolder.Text;

        var result = folderBrowserDialog1.ShowDialog();

        if (result == DialogResult.OK) txtSaveFolder.Text = folderBrowserDialog1.SelectedPath;
    }

    private void btnConfirm_Click(object sender, EventArgs e)
    {
        var saveFolder = txtSaveFolder.Text.Trim();
        var zipFile = chkZipFile.Checked;

        if (Setting == null) Setting = new BackupSetting { DatabaseType = DatabaseType.ToString() };

        Setting.SaveFolder = saveFolder;
        Setting.ZipFile = zipFile;

        if (chkSetAsDefault.Checked)
        {
            var settings = BackupSettingManager.GetSettings();

            var setting = settings.FirstOrDefault(item => item.DatabaseType == DatabaseType.ToString());

            if (setting == null)
            {
                settings.Add(Setting);
            }
            else
            {
                setting.SaveFolder = saveFolder;
                setting.ZipFile = zipFile;
            }

            BackupSettingManager.SaveConfig(settings);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }
}