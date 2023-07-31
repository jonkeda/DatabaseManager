using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Profile;
using Databases.Interpreter.Helper;
using Databases.Model.Connection;
using Databases.Model.Enum;
using Databases.Model.Option;

namespace DatabaseManager.Controls;

public partial class UC_FileConnectionInfo : UserControl
{
    private string databaseVersion;
    public EventHandler OnFileSelect;

    public TestDbConnectHandler OnTestConnect;

    public UC_FileConnectionInfo()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }

    public bool HasPassword => chkHasPassword.Checked;
    public bool RememberPassword => chkRememberPassword.Checked;

    public void LoadData(FileConnectionProfileInfo info, string password = null)
    {
        txtFilePath.Text = info.Database;
        databaseVersion = info.DatabaseVersion;

        if (!string.IsNullOrEmpty(password))
        {
            chkHasPassword.Checked = true;
            chkRememberPassword.Checked = true;

            txtPassword.Text = password;
        }
    }

    private void btnBrowserFile_Click(object sender, EventArgs e)
    {
        openFileDialog1.FileName = "";

        var result = openFileDialog1.ShowDialog();

        if (result == DialogResult.OK)
        {
            txtFilePath.Text = openFileDialog1.FileName;

            OnFileSelect?.Invoke(sender, e);
        }
    }

    private async void btnTest_Click(object sender, EventArgs e)
    {
        await TestConnect();
    }

    public bool ValidateInfo()
    {
        if (string.IsNullOrEmpty(txtFilePath.Text))
        {
            MessageBox.Show("File path can't be empty.");
            return false;
        }

        if (chkHasPassword.Checked && string.IsNullOrEmpty(txtPassword.Text.Trim()))
        {
            MessageBox.Show("Password can't be empty.");
            return false;
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

                databaseVersion = dbConnection.ServerVersion;

                MessageBox.Show("Success.");

                OnTestConnect?.Invoke();

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
        var connectionInfo = new ConnectionInfo();

        connectionInfo.Database = txtFilePath.Text.Trim();

        var password = txtPassword.Text.Trim();

        if (chkHasPassword.Checked && !string.IsNullOrEmpty(password)) connectionInfo.Password = password;

        if (!string.IsNullOrEmpty(databaseVersion)) connectionInfo.ServerVersion = databaseVersion;

        return connectionInfo;
    }

    private void chkHasPassword_CheckedChanged(object sender, EventArgs e)
    {
        var @checked = chkHasPassword.Checked;

        txtPassword.Enabled = @checked;
        chkRememberPassword.Enabled = @checked;

        if (!@checked)
        {
            txtPassword.Text = "";
            chkRememberPassword.Checked = false;
        }
    }
}