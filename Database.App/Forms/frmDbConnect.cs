using System;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Data;
using DatabaseManager.Profile;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Manager;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;

namespace DatabaseManager;

public partial class frmDbConnect : Form
{
    private readonly bool isAdd = true;
    private readonly bool requriePassword;

    public frmDbConnect(DatabaseType dbType)
    {
        InitializeComponent();
        isAdd = true;
        DatabaseType = dbType;
    }

    public frmDbConnect(DatabaseType dbType, string profileName, bool requriePassword = false)
    {
        InitializeComponent();

        isAdd = false;
        this.requriePassword = requriePassword;
        DatabaseType = dbType;
        ProflieName = profileName;
    }

    public DatabaseType DatabaseType { get; set; }
    public string ProflieName { get; set; }
    public bool NotUseProfile { get; set; }

    public ConnectionInfo ConnectionInfo { get; set; }

    private void frmDbConnect_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        ucDbAccountInfo.OnTestConnect += TestConnect;
        ucDbAccountInfo.DatabaseType = DatabaseType;
        ucDbAccountInfo.InitControls();

        if (!NotUseProfile)
        {
            if (string.IsNullOrEmpty(ProflieName))
            {
                txtProfileName.Text = "";
            }
            else
            {
                txtProfileName.Text = ProflieName;
                LoadProfile();
            }
        }
        else
        {
            lblProfileName.Visible = false;
            txtProfileName.Visible = false;
        }
    }

    private async void LoadProfile()
    {
        var connectionInfo = await ConnectionProfileManager.GetConnectionInfo(DatabaseType.ToString(), ProflieName);

        ucDbAccountInfo.LoadData(connectionInfo, ConnectionInfo?.Password);

        cboDatabase.Text = connectionInfo.Database;
    }

    private void TestConnect()
    {
        PopulateDatabases();
    }

    private async void PopulateDatabases()
    {
        var connectionInfo = GetConnectionInfo();
        var dbInterpreter =
            DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, new DbInterpreterOption());

        var oldDatabase = cboDatabase.Text;

        try
        {
            cboDatabase.Items.Clear();

            var databaseses = await dbInterpreter.GetDatabasesAsync();

            databaseses.ForEach(item => { cboDatabase.Items.Add(item.Name); });

            cboDatabase.Text = oldDatabase;

            cboDatabase.DroppedDown = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed:" + ex.Message);
        }
    }

    public ConnectionInfo GetConnectionInfo()
    {
        var connectionInfo = ucDbAccountInfo.GetConnectionInfo();
        connectionInfo.Database = cboDatabase.Text;

        return connectionInfo;
    }

    private async void btnConfirm_Click(object sender, EventArgs e)
    {
        if (!ucDbAccountInfo.ValidateInfo()) return;

        if (string.IsNullOrEmpty(cboDatabase.Text))
        {
            MessageBox.Show("Please select a database.");
            return;
        }

        var profileName = txtProfileName.Text.Trim();
        var database = cboDatabase.Text;

        ConnectionInfo = GetConnectionInfo();

        if (!NotUseProfile)
        {
            var profiles = await ConnectionProfileManager.GetProfiles(DatabaseType.ToString());

            string oldAccountProfileId = null;

            if (!string.IsNullOrEmpty(profileName) && profiles.Any(item => item.Name == profileName))
            {
                var msg = $"The profile name \"{profileName}\" has been existed";

                if (isAdd)
                {
                    var dialogResult = MessageBox.Show(msg + ", are you sure to override it.", "Confirm",
                        MessageBoxButtons.YesNo);

                    if (dialogResult != DialogResult.Yes)
                    {
                        DialogResult = DialogResult.None;
                        return;
                    }
                }
                else if (!isAdd && ProflieName != profileName)
                {
                    MessageBox.Show(msg + ", please edit that.");
                    return;
                }
                else //edit
                {
                    oldAccountProfileId = profiles.FirstOrDefault(item => item.Name == profileName).AccountId;
                }
            }
            else
            {
                var accountProfile = await AccountProfileManager.GetProfile(DatabaseType.ToString(),
                    ConnectionInfo.Server, ConnectionInfo.Port, ConnectionInfo.IntegratedSecurity,
                    ConnectionInfo.UserId);

                if (accountProfile != null) oldAccountProfileId = accountProfile.Id;
            }

            var profile = new ConnectionProfileInfo
            {
                AccountId = oldAccountProfileId,
                DatabaseType = DatabaseType.ToString(),
                Server = ConnectionInfo.Server,
                Port = ConnectionInfo.Port,
                Database = ConnectionInfo.Database,
                IntegratedSecurity = InvokeRequired,
                UserId = ConnectionInfo.UserId,
                Password = ConnectionInfo.Password,
                IsDba = ConnectionInfo.IsDba,
                UseSsl = ConnectionInfo.UseSsl
            };

            if (!string.IsNullOrEmpty(oldAccountProfileId)) profile.AccountId = oldAccountProfileId;

            profile.Name = profileName;
            profile.DatabaseType = DatabaseType.ToString();

            ProflieName = await ConnectionProfileManager.Save(profile, ucDbAccountInfo.RememberPassword);

            if (SettingManager.Setting.RememberPasswordDuringSession)
                if (!ConnectionInfo.IntegratedSecurity && !ucDbAccountInfo.RememberPassword &&
                    !string.IsNullOrEmpty(ConnectionInfo.Password))
                {
                    var accountProfileInfo = new AccountProfileInfo { Id = profile.AccountId };

                    ObjectHelper.CopyProperties(ConnectionInfo, accountProfileInfo);
                    accountProfileInfo.Password = ConnectionInfo.Password;

                    DataStore.SetAccountProfileInfo(accountProfileInfo);
                }
        }

        DialogResult = DialogResult.OK;
    }

    private void frmDbConnect_Activated(object sender, EventArgs e)
    {
        if (requriePassword) ucDbAccountInfo.FocusPasswordTextbox();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void cboDatabase_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(txtProfileName.Text) && !string.IsNullOrEmpty(cboDatabase.Text))
            txtProfileName.Text = cboDatabase.Text;
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
                    var storeInfo = DataStore.GetAccountProfileInfo(frm.SelectedAccountProfileInfo.Id);

                    if (storeInfo != null && !frm.SelectedAccountProfileInfo.IntegratedSecurity &&
                        !string.IsNullOrEmpty(storeInfo.Password)) password = storeInfo.Password;
                }

                ucDbAccountInfo.LoadData(frm.SelectedAccountProfileInfo, password);
            }
        }
    }


    private void cboDatabase_MouseClick(object sender, MouseEventArgs e)
    {
        if (cboDatabase.Items.Count == 0) PopulateDatabases();
    }
}