using System;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Data;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using DatabaseManager.Profile;

namespace DatabaseManager.Controls;

public partial class UC_DbObjectsNavigator : UserControl
{
    public FeedbackHandler OnFeedback;
    public ShowDbObjectContentHandler OnShowContent;

    public UC_DbObjectsNavigator()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType => ManagerUtil.GetDatabaseType(cboDbType.Text);

    private void UC_DbObjectsNavigator_Load(object sender, EventArgs e)
    {
        InitControls();

        tvDbObjects.OnFeedback += Feedback;
    }

    private void InitControls()
    {
        tvDbObjects.OnShowContent += ShowContent;
        LoadDbTypes();
    }

    private void ShowContent(DatabaseObjectDisplayInfo content)
    {
        if (OnShowContent != null) OnShowContent(content);
    }

    private void Feedback(FeedbackInfo info)
    {
        if (OnFeedback != null) OnFeedback(info);
    }

    public void LoadDbTypes()
    {
        var databaseTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();
        foreach (var value in databaseTypes) cboDbType.Items.Add(value.ToString());

        if (cboDbType.Items.Count > 0)
        {
            cboDbType.Text = SettingManager.Setting.PreferredDatabase.ToString();

            if (string.IsNullOrEmpty(cboDbType.Text)) cboDbType.SelectedIndex = 0;
        }

        btnConnect.Focus();
    }

    private void cboDbType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var hasValue = cboDbType.SelectedIndex >= 0;
        btnAddAccount.Enabled = hasValue;

        if (hasValue)
        {
            var databaseType = ManagerUtil.GetDatabaseType(cboDbType.Text);

            if (!ManagerUtil.IsFileConnection(databaseType))
                LoadAccounts();
            else
                LoadFileConnections();
        }
    }

    private async void LoadAccounts(string defaultValue = null)
    {
        var type = cboDbType.Text;

        var profiles = (await AccountProfileManager.GetProfiles(type)).OrderBy(item => item.Description);

        cboAccount.DataSource = profiles.ToList();
        cboAccount.DisplayMember = nameof(AccountProfileInfo.Description);
        cboAccount.ValueMember = nameof(AccountProfileInfo.Id);

        var ids = profiles.Select(item => item.Id).ToList();

        if (string.IsNullOrEmpty(defaultValue))
        {
            if (profiles.Count() > 0) cboAccount.SelectedIndex = 0;
        }
        else
        {
            if (ids.Contains(defaultValue))
                cboAccount.Text = profiles.FirstOrDefault(item => item.Id == defaultValue)?.Description;
        }

        btnConnect.Enabled = cboAccount.Items.Count > 0;
    }

    private async void LoadFileConnections(string defaultValue = null)
    {
        var type = cboDbType.Text;

        var profiles = (await FileConnectionProfileManager.GetProfiles(type)).OrderBy(item => item.Description);

        cboAccount.DataSource = profiles.ToList();
        cboAccount.DisplayMember = nameof(FileConnectionProfileInfo.Description);
        cboAccount.ValueMember = nameof(FileConnectionProfileInfo.Id);

        var ids = profiles.Select(item => item.Id).ToList();

        if (string.IsNullOrEmpty(defaultValue))
        {
            if (profiles.Count() > 0) cboAccount.SelectedIndex = 0;
        }
        else
        {
            if (ids.Contains(defaultValue))
                cboAccount.Text = profiles.FirstOrDefault(item => item.Id == defaultValue)?.Description;
        }

        btnConnect.Enabled = cboAccount.Items.Count > 0;
    }

    private void btnAddAccount_Click(object sender, EventArgs e)
    {
        var databaseType = cboDbType.Text;

        if (string.IsNullOrEmpty(databaseType))
        {
            MessageBox.Show("Please select a database type first.");
        }
        else
        {
            var dbType = ManagerUtil.GetDatabaseType(databaseType);

            if (!ManagerUtil.IsFileConnection(dbType))
            {
                var form = new frmAccountInfo(dbType);
                var result = form.ShowDialog();

                if (result == DialogResult.OK)
                {
                    LoadAccounts(form.AccountProfileId);

                    if (cboAccount.SelectedItem != null)
                        (cboAccount.SelectedItem as AccountProfileInfo).Password = form.AccountProfileInfo.Password;
                }
            }
            else
            {
                var form = new frmFileConnection(dbType);

                var result = form.ShowDialog();

                if (result == DialogResult.OK)
                {
                    LoadFileConnections(form.FileConnectionProfileId);

                    if (cboAccount.SelectedItem != null)
                        (cboAccount.SelectedItem as FileConnectionProfileInfo).Password =
                            form.FileConnectionProfileInfo.Password;
                }
            }
        }
    }

    private void btnConnect_Click(object sender, EventArgs e)
    {
        Invoke(Connect);
    }

    private async void Connect()
    {
        var selectedItem = cboAccount.SelectedItem;

        var connectionInfo = new ConnectionInfo();

        AccountProfileInfo accountProfileInfo = null;
        FileConnectionProfileInfo fileConnectionProfileInfo = null;

        if (selectedItem is AccountProfileInfo)
        {
            accountProfileInfo = selectedItem as AccountProfileInfo;

            if (!accountProfileInfo.IntegratedSecurity && string.IsNullOrEmpty(accountProfileInfo.Password))
            {
                var storedInfo = DataStore.GetAccountProfileInfo(accountProfileInfo.Id);

                if (storedInfo != null && !string.IsNullOrEmpty(storedInfo.Password))
                {
                    accountProfileInfo.Password = storedInfo.Password;
                }
                else
                {
                    MessageBox.Show("Please specify password for the database.");

                    if (!SetConnectionInfo(accountProfileInfo)) return;
                }
            }

            ObjectHelper.CopyProperties(accountProfileInfo, connectionInfo);
        }
        else if (selectedItem is FileConnectionProfileInfo)
        {
            fileConnectionProfileInfo = selectedItem as FileConnectionProfileInfo;

            if (fileConnectionProfileInfo.HasPassword && string.IsNullOrEmpty(fileConnectionProfileInfo.Password))
            {
                var storedInfo = DataStore.GetFileConnectionProfileInfo(fileConnectionProfileInfo.Id);

                if (storedInfo != null && !string.IsNullOrEmpty(storedInfo.Password))
                {
                    fileConnectionProfileInfo.Password = storedInfo.Password;
                }
                else
                {
                    MessageBox.Show("Please specify password for the database.");

                    if (!SetFileConnectionInfo(fileConnectionProfileInfo)) return;
                }
            }

            ObjectHelper.CopyProperties(fileConnectionProfileInfo, connectionInfo);
        }

        btnConnect.Enabled = false;

        try
        {
            await tvDbObjects.LoadTree(DatabaseType, connectionInfo);

            if (SettingManager.Setting.RememberPasswordDuringSession)
            {
                if (accountProfileInfo != null)
                    DataStore.SetAccountProfileInfo(accountProfileInfo);
                else if (fileConnectionProfileInfo != null)
                    DataStore.SetFileConnectionProfileInfo(fileConnectionProfileInfo);
            }
        }
        catch (Exception ex)
        {
            tvDbObjects.ClearNodes();

            var message = ExceptionHelper.GetExceptionDetails(ex);

            LogHelper.LogError(message);

            MessageBox.Show("Error:" + message);

            if (accountProfileInfo != null && !SetConnectionInfo(accountProfileInfo))
                return;
            if (fileConnectionProfileInfo != null && !SetFileConnectionInfo(fileConnectionProfileInfo))
                return;
            Connect();
        }

        btnConnect.Enabled = true;
    }

    private bool SetConnectionInfo(AccountProfileInfo accountProfileInfo)
    {
        var dbType = ManagerUtil.GetDatabaseType(cboDbType.Text);

        var frmAccountInfo = new frmAccountInfo(dbType, true) { AccountProfileInfo = accountProfileInfo };

        var dialogResult = frmAccountInfo.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
            var profileInfo = frmAccountInfo.AccountProfileInfo;

            ObjectHelper.CopyProperties(profileInfo, cboAccount.SelectedItem as AccountProfileInfo);
            cboAccount.Text = profileInfo.Description;

            return true;
        }

        btnConnect.Enabled = true;

        return false;
    }

    private bool SetFileConnectionInfo(FileConnectionProfileInfo fileConnectionProfileInfo)
    {
        var dbType = ManagerUtil.GetDatabaseType(cboDbType.Text);

        var form = new frmFileConnection(dbType, true) { FileConnectionProfileInfo = fileConnectionProfileInfo };

        var dialogResult = form.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
            var profileInfo = form.FileConnectionProfileInfo;

            ObjectHelper.CopyProperties(profileInfo, cboAccount.SelectedItem as FileConnectionProfileInfo);
            cboAccount.Text = profileInfo.Description;

            return true;
        }

        btnConnect.Enabled = true;

        return false;
    }

    public ConnectionInfo GetCurrentConnectionInfo()
    {
        return tvDbObjects.GetCurrentConnectionInfo();
    }

    public DatabaseObjectDisplayInfo GetDisplayInfo()
    {
        var info = tvDbObjects.GetDisplayInfo();

        info.DatabaseType = DatabaseType;

        return info;
    }

    private void UC_DbObjectsNavigator_SizeChanged(object sender, EventArgs e)
    {
        cboDbType.Refresh();
        cboAccount.Refresh();
    }
}