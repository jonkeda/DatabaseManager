using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;
using DatabaseManager.Profile;

namespace DatabaseManager.Controls;

public delegate void TestDbConnectHandler();

public partial class UC_DbAccountInfo : UserControl
{
    public TestDbConnectHandler OnTestConnect;
    private string serverVersion;

    public UC_DbAccountInfo()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }

    public bool RememberPassword => chkRememberPassword.Checked;

    public async void InitControls()
    {
        if (DatabaseType == DatabaseType.MySql)
        {
            lblPort.Visible = txtPort.Visible = true;
            txtPort.Text = MySqlInterpreter.DEFAULT_PORT.ToString();
            chkUseSsl.Visible = true;
        }
        else if (DatabaseType == DatabaseType.Oracle)
        {
            lblPort.Visible = txtPort.Visible = true;
            txtPort.Text = OracleInterpreter.DEFAULT_PORT.ToString();
        }
        else if (DatabaseType == DatabaseType.Postgres)
        {
            lblPort.Visible = txtPort.Visible = true;
            txtPort.Text = PostgresInterpreter.DEFAULT_PORT.ToString();
        }

        var authTypes = Enum.GetNames(typeof(AuthenticationType));
        cboAuthentication.Items.AddRange(authTypes);

        if (DatabaseType != DatabaseType.SqlServer)
            cboAuthentication.Text = AuthenticationType.Password.ToString();
        //this.cboAuthentication.Enabled = false;
        else
            cboAuthentication.Text = AuthenticationType.IntegratedSecurity.ToString();

        chkAsDba.Visible = DatabaseType == DatabaseType.Oracle;

        var profiles = await AccountProfileManager.GetProfiles(DatabaseType.ToString());
        var serverNames = profiles.Select(item => item.Server).Distinct().OrderBy(item => item).ToArray();
        cboServer.Items.AddRange(serverNames);
    }

    public void LoadData(DatabaseAccountInfo info, string password = null)
    {
        cboServer.Text = info.Server;
        txtPort.Text = info.Port;
        cboAuthentication.Text = info.IntegratedSecurity
            ? AuthenticationType.IntegratedSecurity.ToString()
            : AuthenticationType.Password.ToString();
        txtUserId.Text = info.UserId;
        txtPassword.Text = info.Password;
        chkAsDba.Checked = info.IsDba;
        chkUseSsl.Checked = info.UseSsl;
        serverVersion = info.ServerVersion;

        if (info.IntegratedSecurity)
        {
            cboAuthentication.Text = AuthenticationType.IntegratedSecurity.ToString();
        }
        else
        {
            if (!string.IsNullOrEmpty(password)) txtPassword.Text = password;

            if (!string.IsNullOrEmpty(info.Password)) chkRememberPassword.Checked = true;
        }
    }

    public bool ValidateInfo()
    {
        if (string.IsNullOrEmpty(cboServer.Text))
        {
            MessageBox.Show("Server name can't be empty.");
            return false;
        }

        if (string.IsNullOrEmpty(cboAuthentication.Text))
        {
            MessageBox.Show("Please select a authentication type.");
            return false;
        }

        if (cboAuthentication.Text == AuthenticationType.Password.ToString())
        {
            if (string.IsNullOrEmpty(txtUserId.Text))
            {
                MessageBox.Show("User name can't be empty.");
                return false;
            }

            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                MessageBox.Show("Password can't be empty.");
                return false;
            }
        }

        return true;
    }

    public async Task<bool> TestConnect()
    {
        if (!ValidateInfo()) return false;

        var connectionInfo = GetConnectionInfo();

        var dbInterpreter =
            DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, new DbInterpreterOption());

        try
        {
            using (var dbConnection = dbInterpreter.CreateConnection())
            {
                await dbConnection.OpenAsync();

                serverVersion = dbConnection.ServerVersion;

                MessageBox.Show("Success.");

                if (OnTestConnect != null) OnTestConnect();

                return true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed:" + ex.Message);
            return false;
        }
    }

    public ConnectionInfo GetConnectionInfo()
    {
        var connectionInfo = new ConnectionInfo
        {
            Server = cboServer.Text.Trim(),
            Port = txtPort.Text.Trim(),
            IntegratedSecurity = cboAuthentication.Text != AuthenticationType.Password.ToString(),
            UserId = txtUserId.Text.Trim(),
            Password = txtPassword.Text.Trim(),
            IsDba = chkAsDba.Checked,
            UseSsl = chkUseSsl.Checked
        };

        if (!string.IsNullOrEmpty(serverVersion)) connectionInfo.ServerVersion = serverVersion;

        return connectionInfo;
    }

    private void cboAuthentication_SelectedIndexChanged(object sender, EventArgs e)
    {
        var isWindowsAuth = cboAuthentication.Text == AuthenticationType.IntegratedSecurity.ToString();

        txtUserId.Enabled = !isWindowsAuth;
        txtPassword.Enabled = !isWindowsAuth;
        chkRememberPassword.Enabled = !isWindowsAuth;

        chkRememberPassword.Checked = false;
        txtUserId.Text = txtPassword.Text = "";
    }

    public void FocusPasswordTextbox()
    {
        txtPassword.Focus();
    }

    private async void btnTest_Click(object sender, EventArgs e)
    {
        await TestConnect();
    }
}