using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Data;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Profile;

namespace DatabaseManager.Controls;

public delegate void SelectedChangeHandler(object sender, ConnectionInfo connectionInfo);

public partial class UC_DbConnectionProfile : UserControl
{
    private bool isDataBinding;

    public UC_DbConnectionProfile()
    {
        InitializeComponent();
    }

    public ConnectionInfo ConnectionInfo { get; private set; }

    [Category("Title")]
    public string Title
    {
        get => lblTitle.Text;
        set => lblTitle.Text = value;
    }

    public int ClientHeight => btnAddDbProfile.Height;

    public DatabaseType DatabaseType
    {
        get => ManagerUtil.GetDatabaseType(cboDbType.Text);
        set => cboDbType.Text = value.ToString();
    }

    public bool EnableDatabaseType
    {
        get => cboDbType.Enabled;
        set => cboDbType.Enabled = value;
    }

    public event SelectedChangeHandler OnSelectedChanged;

    private void UC_DbConnectionProfile_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        LoadDbTypes();
    }

    public void LoadDbTypes()
    {
        var databaseTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

        foreach (var value in databaseTypes) cboDbType.Items.Add(value.ToString());
    }

    public bool IsDbTypeSelected()
    {
        return !string.IsNullOrEmpty(cboDbType.Text);
    }

    public bool IsProfileSelected()
    {
        return !string.IsNullOrEmpty(cboDbProfile.Text);
    }

    private void cboDbProfile_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (!isDataBinding) GetConnectionInfoByProfile();
    }

    private async void GetConnectionInfoByProfile()
    {
        var dbType = DatabaseType;

        if (!ManagerUtil.IsFileConnection(dbType))
        {
            var profileName = (cboDbProfile.SelectedItem as ConnectionProfileInfo)?.Name;

            var connectionInfo = await ConnectionProfileManager.GetConnectionInfo(dbType.ToString(), profileName);

            if (connectionInfo != null)
            {
                SetConnectionPasswordFromDataStore(connectionInfo);

                OnSelectedChanged?.Invoke(this, connectionInfo);
            }
        }
        else
        {
            var id = (cboDbProfile.SelectedItem as FileConnectionProfileInfo)?.Id;

            var profile = await FileConnectionProfileManager.GetProfileById(id);

            if (profile != null)
            {
                var connectionInfo = new ConnectionInfo();

                ObjectHelper.CopyProperties(profile, connectionInfo);

                SetFileConnectionPasswordFromDataStore(connectionInfo);

                OnSelectedChanged?.Invoke(this, connectionInfo);
            }
        }
    }

    private async void LoadProfileNames(string defaultValue = null)
    {
        var dbType = cboDbType.Text;

        if (dbType != "")
        {
            IEnumerable<dynamic> profiles = null;
            List<string> names = null;

            string displayMember = null;
            string valueMember = null;

            if (!ManagerUtil.IsFileConnection(DatabaseType))
            {
                profiles = await ConnectionProfileManager.GetProfiles(dbType);
                names = profiles.Select(item => (item as ConnectionProfileInfo).Name).ToList();

                displayMember = nameof(ConnectionProfileInfo.Description);
                valueMember = nameof(ConnectionProfileInfo.Name);
            }
            else
            {
                profiles = await FileConnectionProfileManager.GetProfiles(dbType);
                names = profiles.Select(item => (item as FileConnectionProfileInfo).Name).ToList();

                displayMember = nameof(FileConnectionProfileInfo.Description);
                valueMember = nameof(FileConnectionProfileInfo.Name);
            }

            cboDbProfile.Items.Clear();

            isDataBinding = true;

            cboDbProfile.DisplayMember = displayMember;
            cboDbProfile.ValueMember = valueMember;

            foreach (var profile in profiles) cboDbProfile.Items.Add(profile);

            isDataBinding = false;

            if (string.IsNullOrEmpty(defaultValue))
            {
                if (profiles.Count() > 0) cboDbProfile.SelectedIndex = 0;
            }
            else
            {
                if (names.Contains(defaultValue))
                    cboDbProfile.Text = profiles.FirstOrDefault(item => item.Name == defaultValue)?.Description;
            }

            var selected = cboDbProfile.Text.Length > 0;

            btnConfigDbProfile.Visible = btnDeleteDbProfile.Visible = selected;

            GetConnectionInfoByProfile();
        }
    }

    private void cboDbProfile_DrawItem(object sender, DrawItemEventArgs e)
    {
        var combobox = sender as ComboBox;
        if (combobox.DroppedDown) e.DrawBackground();

        e.DrawFocusRectangle();

        var items = combobox.Items;

        if (e.Index < 0)
        {
            e.Graphics.DrawString(combobox.Text, e.Font, new SolidBrush(e.ForeColor), e.Bounds.Left, e.Bounds.Y);
        }
        else
        {
            if (items.Count > 0 && e.Index < items.Count)
            {
                var obj = items[e.Index];

                string descrition = null;

                if (obj is ConnectionProfileInfo cpi)
                    descrition = cpi.Description;
                else if (obj is FileConnectionProfileInfo fcpi) descrition = fcpi.Description;

                e.Graphics.DrawString(descrition, e.Font,
                    new SolidBrush(combobox.DroppedDown ? e.ForeColor : Color.Black), e.Bounds.Left, e.Bounds.Y);
            }
        }
    }

    private void btnAddDbProfile_Click(object sender, EventArgs e)
    {
        AddConnection(true, cboDbType.Text);
    }

    private void AddConnection(bool isSource, string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            MessageBox.Show("Please select database type.");
            return;
        }

        var dbType = DatabaseType;

        if (!ManagerUtil.IsFileConnection(dbType))
        {
            var form = new frmDbConnect(dbType);

            if (SetConnectionInfo(form)) LoadProfileNames(form.ProflieName);
        }
        else
        {
            var form = new frmFileConnection(dbType) { ShowChooseControls = true };

            if (SetFileConnectionInfo(form)) LoadProfileNames(form.FileConnectionProfileInfo.Name);
        }
    }

    private bool SetConnectionInfo(frmDbConnect frmDbConnect)
    {
        var dialogResult = frmDbConnect.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
            var connectionInfo = frmDbConnect.ConnectionInfo;

            SetConnectionPasswordFromDataStore(connectionInfo);

            ConnectionInfo = connectionInfo;

            var profileInfo = cboDbProfile.SelectedItem as ConnectionProfileInfo;

            if (profileInfo != null)
                if (!profileInfo.IntegratedSecurity && string.IsNullOrEmpty(profileInfo.Password) &&
                    !string.IsNullOrEmpty(connectionInfo.Password))
                    profileInfo.Password = connectionInfo.Password;

            OnSelectedChanged?.Invoke(this, connectionInfo);

            return true;
        }

        return false;
    }

    private bool SetFileConnectionInfo(frmFileConnection frmFileConnect)
    {
        var dialogResult = frmFileConnect.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
            var connectionInfo = frmFileConnect.ConnectionInfo;

            SetFileConnectionPasswordFromDataStore(connectionInfo);

            ConnectionInfo = connectionInfo;

            var profileInfo = cboDbProfile.SelectedItem as FileConnectionProfileInfo;

            if (profileInfo != null)
                if (string.IsNullOrEmpty(profileInfo.Password) && !string.IsNullOrEmpty(connectionInfo.Password))
                    profileInfo.Password = connectionInfo.Password;

            OnSelectedChanged?.Invoke(this, connectionInfo);

            return true;
        }

        return false;
    }

    private void SetConnectionPasswordFromDataStore(ConnectionInfo connectionInfo)
    {
        if (!SettingManager.Setting.RememberPasswordDuringSession || connectionInfo.IntegratedSecurity) return;

        var profileInfo = cboDbProfile.SelectedItem as ConnectionProfileInfo;

        if (profileInfo != null)
            if (string.IsNullOrEmpty(connectionInfo.Password))
            {
                var accountProfile = DataStore.GetAccountProfileInfo(profileInfo.AccountId);

                if (accountProfile != null && !accountProfile.IntegratedSecurity &&
                    !string.IsNullOrEmpty(accountProfile.Password))
                {
                    connectionInfo.Password = accountProfile.Password;

                    if (string.IsNullOrEmpty(profileInfo.Password)) profileInfo.Password = accountProfile.Password;
                }
            }
    }

    private void SetFileConnectionPasswordFromDataStore(ConnectionInfo connectionInfo)
    {
        if (!SettingManager.Setting.RememberPasswordDuringSession) return;

        var profileInfo = cboDbProfile.SelectedItem as FileConnectionProfileInfo;

        if (profileInfo != null)
            if (string.IsNullOrEmpty(connectionInfo.Password))
            {
                var profile = DataStore.GetFileConnectionProfileInfo(profileInfo.Id);

                if (profile != null && !string.IsNullOrEmpty(profile.Password))
                {
                    connectionInfo.Password = profile.Password;

                    if (string.IsNullOrEmpty(profileInfo.Password)) profileInfo.Password = profile.Password;
                }
            }
    }

    private void btnConfigDbProfile_Click(object sender, EventArgs e)
    {
        ConfigConnection();
    }

    public void ConfigConnection(bool requriePassword = false)
    {
        var type = cboDbType.Text;
        var selectedItem = cboDbProfile.SelectedItem;

        if (string.IsNullOrEmpty(type))
        {
            MessageBox.Show("Please select database type.");
            return;
        }

        if (selectedItem == null || string.IsNullOrEmpty(cboDbProfile.Text))
        {
            MessageBox.Show("Please select a profile.");
            return;
        }

        var dbType = DatabaseType;

        if (!ManagerUtil.IsFileConnection(dbType))
        {
            var profile = selectedItem as ConnectionProfileInfo;
            var profileName = profile.Name;

            var frm = new frmDbConnect(dbType, profileName, requriePassword);
            frm.ConnectionInfo = GetConnectionInfo(profile);

            SetConnectionInfo(frm);

            if (profileName != frm.ProflieName) LoadProfileNames(frm.ProflieName);

            if (cboDbProfile.SelectedItem != null)
            {
                var p = cboDbProfile.SelectedItem as ConnectionProfileInfo;

                SetProfileConnectionInfo(p, frm.ConnectionInfo);
            }
        }
        else
        {
            var profile = selectedItem as FileConnectionProfileInfo;
            var profileName = profile.Name;

            var frm = new frmFileConnection(dbType, requriePassword) { ShowChooseControls = true };
            frm.FileConnectionProfileInfo = profile;

            SetFileConnectionInfo(frm);

            if (profileName != frm.FileConnectionProfileInfo?.Name)
                LoadProfileNames(frm.FileConnectionProfileInfo?.Name);

            if (cboDbProfile.SelectedItem != null)
            {
                var p = cboDbProfile.SelectedItem as FileConnectionProfileInfo;

                SetFileProfileConnectionInfo(p, frm.ConnectionInfo);
            }
        }
    }

    private ConnectionInfo GetConnectionInfo(ConnectionProfileInfo profile)
    {
        if (profile != null)
            return new ConnectionInfo
            {
                Server = profile.Server, Port = profile.Port, Database = profile.Database,
                IntegratedSecurity = profile.IntegratedSecurity, UserId = profile.UserId, Password = profile.Password,
                IsDba = profile.IsDba, UseSsl = profile.UseSsl
            };

        return null;
    }


    private void SetProfileConnectionInfo(ConnectionProfileInfo profile, ConnectionInfo connectionInfo)
    {
        if (connectionInfo != null)
        {
            profile.Server = connectionInfo.Server;
            profile.Port = connectionInfo.Port;
            profile.Database = connectionInfo.Database;
            profile.IntegratedSecurity = connectionInfo.IntegratedSecurity;
            profile.UserId = connectionInfo.UserId;
            profile.Password = connectionInfo.Password;
            profile.IsDba = connectionInfo.IsDba;
            profile.UseSsl = connectionInfo.UseSsl;
        }
    }

    private void SetFileProfileConnectionInfo(FileConnectionProfileInfo profile, ConnectionInfo connectionInfo)
    {
        if (connectionInfo != null)
        {
            profile.Database = connectionInfo.Database;
            profile.Password = connectionInfo.Password;
        }
    }

    private void btnDeleteDbProfile_Click(object sender, EventArgs e)
    {
        DeleteProfile();
    }

    private async void DeleteProfile()
    {
        var dialogResult = MessageBox.Show("Are you sure to delete the profile?", "Confirm", MessageBoxButtons.YesNo);

        if (dialogResult == DialogResult.Yes)
        {
            var success = false;

            if (!ManagerUtil.IsFileConnection(DatabaseType))
            {
                var id = (cboDbProfile.SelectedItem as ConnectionProfileInfo).Id;
                success = await ConnectionProfileManager.Delete(new[] { id });
            }
            else
            {
                var id = (cboDbProfile.SelectedItem as FileConnectionProfileInfo).Id;
                success = await FileConnectionProfileManager.Delete(new[] { id });
            }

            if (success) LoadProfileNames();
        }
    }

    private void cboDbType_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadProfileNames();
    }

    public bool ValidateProfile()
    {
        var profileInfo = cboDbProfile.SelectedItem as ConnectionProfileInfo;

        if (profileInfo != null)
            if (!profileInfo.IntegratedSecurity && string.IsNullOrEmpty(profileInfo.Password))
            {
                MessageBox.Show("Please specify the password.");

                ConfigConnection(true);

                return false;
            }

        return true;
    }
}