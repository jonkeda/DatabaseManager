using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Manager.Manager;
using Databases.Manager.Model.Setting;
using Databases.Model.Enum;

namespace DatabaseManager;

public partial class frmBackupSetting : Form
{
    public frmBackupSetting()
    {
        InitializeComponent();

        dgvSettings.AutoGenerateColumns = false;
    }

    private void frmBackupSetting_Load(object sender, EventArgs e)
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = BackupSettingManager.GetSettings();

        var dbTypes = Enum.GetNames(typeof(DatabaseType));

        foreach (var dbType in dbTypes)
            if (dbType != DatabaseType.Unknown.ToString())
            {
                var setting = settings.FirstOrDefault(item => item.DatabaseType == dbType);

                if (setting == null) settings.Add(new BackupSetting { DatabaseType = dbType });
            }

        dgvSettings.DataSource = settings;
    }

    private void dgvSettings_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var cell = dgvSettings.Rows[e.RowIndex].Cells[e.ColumnIndex];

        if (cell.ReadOnly) return;

        var value = DataGridViewHelper.GetCellStringValue(cell);

        if (e.ColumnIndex == colClientToolFilePath.Index)
        {
            if (openFileDialog1 == null) openFileDialog1 = new OpenFileDialog();

            if (!string.IsNullOrEmpty(value) && File.Exists(value))
                openFileDialog1.FileName = value;
            else
                openFileDialog1.FileName = "";

            var result = openFileDialog1.ShowDialog();

            if (result == DialogResult.OK) SetCellValue(cell, openFileDialog1.FileName);
        }
        else if (e.ColumnIndex == colSaveFolder.Index)
        {
            if (folderBrowserDialog1 == null) folderBrowserDialog1 = new FolderBrowserDialog();

            if (!string.IsNullOrEmpty(value) && File.Exists(value))
                folderBrowserDialog1.SelectedPath = value;
            else
                folderBrowserDialog1.SelectedPath = "";

            var result = folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK) SetCellValue(cell, folderBrowserDialog1.SelectedPath);
        }
    }

    private void SetCellValue(DataGridViewCell cell, string value)
    {
        cell.Value = value;

        dgvSettings.EndEdit();
        dgvSettings.CurrentCell = null;
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        Save();

        MessageBox.Show("Saved successfully.");

        DialogResult = DialogResult.OK;

        Close();
    }

    private List<BackupSetting> GetSettings()
    {
        var settings = new List<BackupSetting>();

        foreach (DataGridViewRow row in dgvSettings.Rows)
        {
            var setting = new BackupSetting();
            setting.DatabaseType = DataGridViewHelper.GetCellStringValue(row, colDatabaseType.Name);
            setting.ClientToolFilePath = DataGridViewHelper.GetCellStringValue(row, colClientToolFilePath.Name);
            setting.SaveFolder = DataGridViewHelper.GetCellStringValue(row, colSaveFolder.Name);
            setting.ZipFile = DataGridViewHelper.GetCellBoolValue(row, colZipBackupFile.Name);

            settings.Add(setting);
        }

        return settings;
    }

    private void Save()
    {
        BackupSettingManager.SaveConfig(GetSettings());
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void dgvSettings_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        foreach (DataGridViewRow row in dgvSettings.Rows)
        {
            var dbType = DataGridViewHelper.GetCellStringValue(row, colDatabaseType.Name);

            if (dbType == DatabaseType.SqlServer.ToString())
            {
                row.Cells[colClientToolFilePath.Name].ReadOnly = true;
                row.Cells[colZipBackupFile.Name].ReadOnly = true;
            }
            else if (dbType == DatabaseType.Postgres.ToString())
            {
                row.Cells[colZipBackupFile.Name].ReadOnly = true;
            }
        }

        dgvSettings.ClearSelection();
    }
}