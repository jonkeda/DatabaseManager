using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Data;
using DatabaseManager.Profile;
using Databases.Manager.Manager;
using Databases.Model.Connection;
using Databases.Model.Enum;

namespace DatabaseManager.Forms;

public partial class frmFileConnection : Form
{
    private bool requriePassword;

    public frmFileConnection()
    {
        InitializeComponent();
    }

    public frmFileConnection(DatabaseType dbType)
    {
        InitializeComponent();

        DatabaseType = dbType;
    }

    public frmFileConnection(DatabaseType dbType, bool requriePassword)
    {
        InitializeComponent();

        DatabaseType = dbType;
        this.requriePassword = requriePassword;
    }

    public DatabaseType DatabaseType { get; set; }
    public ConnectionInfo ConnectionInfo { get; set; }
    public bool ShowChooseControls { get; set; }

    public string FileConnectionProfileId { get; set; }

    public FileConnectionProfileInfo FileConnectionProfileInfo { get; set; }

    private void frmFileConnection_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        if (!ShowChooseControls)
        {
            panelChoose.Visible = false;

            var height = panelChoose.Height + 10;

            panelContent.Top -= height;
            Height -= height;
        }

        ucFileConnection.DatabaseType = DatabaseType;

        if (FileConnectionProfileInfo != null)
        {
            ucFileConnection.LoadData(FileConnectionProfileInfo);

            txtDisplayName.Text = FileConnectionProfileInfo.Name;
        }

        ucFileConnection.OnFileSelect += OnFileSelected;
    }

    private void OnFileSelected(object? sender, EventArgs e)
    {
        var connectionInfo = ucFileConnection.GetConnectionInfo();

        if (string.IsNullOrEmpty(txtDisplayName.Text) && !string.IsNullOrEmpty(connectionInfo.Database))
            txtDisplayName.Text = Path.GetFileNameWithoutExtension(connectionInfo.Database);
    }

    private async void btnConfirm_Click(object sender, EventArgs e)
    {
        if (!ucFileConnection.ValidateInfo()) return;

        if (string.IsNullOrEmpty(txtDisplayName.Text.Trim()))
        {
            MessageBox.Show("Display name can't be empty.");
            return;
        }

        var profileInfo = GetFileConnectionProfileInfo();

        var profiles = await FileConnectionProfileManager.GetProfiles(DatabaseType.ToString());

        var isAdd = FileConnectionProfileInfo == null;

        if (isAdd)
        {
            if (profiles.Any(item => item.Database == profileInfo.Database))
            {
                MessageBox.Show($"The record has already existed:{profileInfo.Description}");
                return;
            }
        }
        else
        {
            if (profiles.Where(item => item.Id != FileConnectionProfileInfo.Id)
                .Any(item => item.Database == profileInfo.Database))
            {
                MessageBox.Show($"The record has already existed:{profileInfo.Description}");
                return;
            }
        }

        FileConnectionProfileId =
            await FileConnectionProfileManager.Save(profileInfo, ucFileConnection.RememberPassword);

        FileConnectionProfileInfo = profileInfo;

        DialogResult = DialogResult.OK;

        if (SettingManager.Setting.RememberPasswordDuringSession) DataStore.SetFileConnectionProfileInfo(profileInfo);

        Close();
    }

    private FileConnectionProfileInfo GetFileConnectionProfileInfo()
    {
        var connectionInfo = ucFileConnection.GetConnectionInfo();

        ConnectionInfo = connectionInfo;

        var profileInfo = new FileConnectionProfileInfo
        {
            DatabaseType = DatabaseType.ToString(),
            Database = connectionInfo.Database,
            HasPassword = ucFileConnection.HasPassword,
            Password = connectionInfo.Password,
            Name = txtDisplayName.Text.Trim()
        };

        if (FileConnectionProfileInfo != null) profileInfo.Id = FileConnectionProfileInfo.Id;

        return profileInfo;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void rbChoose_CheckedChanged(object sender, EventArgs e)
    {
        if (rbChoose.Checked)
        {
            var frm = new frmDbConnectionManage(DatabaseType) { IsForSelecting = true };

            if (frm.ShowDialog() == DialogResult.OK)
            {
                string password = null;

                if (SettingManager.Setting.RememberPasswordDuringSession)
                {
                    var storeInfo = DataStore.GetFileConnectionProfileInfo(frm.SelectedFileConnectionProfileInfo.Id);

                    if (storeInfo != null && !string.IsNullOrEmpty(storeInfo.Password)) password = storeInfo.Password;
                }

                ucFileConnection.LoadData(frm.SelectedFileConnectionProfileInfo, password);

                txtDisplayName.Text = frm.SelectedFileConnectionProfileInfo.Name;
            }
        }
    }
}