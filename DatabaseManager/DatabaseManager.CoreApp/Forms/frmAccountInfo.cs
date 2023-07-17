using System;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Data;
using DatabaseManager.Profile;

namespace DatabaseManager;

public partial class frmAccountInfo : Form
{
    private readonly bool requriePassword;

    public frmAccountInfo(DatabaseType dbType)
    {
        InitializeComponent();

        DatabaseType = dbType;
    }

    public frmAccountInfo(DatabaseType dbType, bool requriePassword)
    {
        InitializeComponent();

        DatabaseType = dbType;
        this.requriePassword = requriePassword;
    }

    public DatabaseType DatabaseType { get; set; }
    public string AccountProfileId { get; set; }
    public AccountProfileInfo AccountProfileInfo { get; set; }

    private void frmAccountInfo_Load(object sender, EventArgs e)
    {
        Init();
    }

    private void Init()
    {
        ucAccountInfo.DatabaseType = DatabaseType;
        ucAccountInfo.InitControls();

        if (AccountProfileInfo != null) ucAccountInfo.LoadData(AccountProfileInfo);
    }

    private void frmAccountInfo_Activated(object sender, EventArgs e)
    {
        if (requriePassword) ucAccountInfo.FocusPasswordTextbox();
    }

    private async void btnConfirm_Click(object sender, EventArgs e)
    {
        if (!ucAccountInfo.ValidateInfo()) return;

        var accountProfileInfo = GetAccountProfileInfo();

        var profiles = await AccountProfileManager.GetProfiles(DatabaseType.ToString());

        var isAdd = AccountProfileInfo == null;

        if (isAdd)
        {
            if (profiles.Any(item => item.Server == accountProfileInfo.Server
                                     && item.IntegratedSecurity == accountProfileInfo.IntegratedSecurity
                                     && item.UserId == accountProfileInfo.UserId
                                     && item.Port == accountProfileInfo.Port))
            {
                MessageBox.Show($"The record has already existed:{accountProfileInfo.Description}");
                return;
            }
        }
        else
        {
            if (profiles.Where(item => item.Id != AccountProfileInfo.Id).Any(item =>
                    item.Server == accountProfileInfo.Server
                    && item.IntegratedSecurity == accountProfileInfo.IntegratedSecurity
                    && item.UserId == accountProfileInfo.UserId
                    && item.Port == accountProfileInfo.Port))
            {
                MessageBox.Show($"The record has already existed:{accountProfileInfo.Description}");
                return;
            }
        }

        AccountProfileId = await AccountProfileManager.Save(accountProfileInfo, ucAccountInfo.RememberPassword);

        AccountProfileInfo = accountProfileInfo;

        DialogResult = DialogResult.OK;

        if (SettingManager.Setting.RememberPasswordDuringSession) DataStore.SetAccountProfileInfo(accountProfileInfo);

        Close();
    }

    private AccountProfileInfo GetAccountProfileInfo()
    {
        var connectionInfo = ucAccountInfo.GetConnectionInfo();

        var accountProfileInfo = new AccountProfileInfo { DatabaseType = DatabaseType.ToString() };

        ObjectHelper.CopyProperties(connectionInfo, accountProfileInfo);

        if (AccountProfileInfo != null) accountProfileInfo.Id = AccountProfileInfo.Id;

        return accountProfileInfo;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }
}