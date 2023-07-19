using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Data;
using DatabaseManager.Profile;

namespace DatabaseManager;

public partial class frmLockApp : Form
{
    private bool confirmClose;
    private bool isLocked;

    public frmLockApp()
    {
        InitializeComponent();
    }

    private void frmLockApp_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private async Task InitControls()
    {
        var ps = await PersonalSettingManager.GetPersonalSetting();

        if (ps != null && !string.IsNullOrEmpty(ps.LockPassword)) txtPassword.Text = ps.LockPassword;
    }

    private void frmLockApp_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (!isLocked)
        {
            e.Cancel = false;
            return;
        }

        if (!confirmClose)
        {
            MessageBox.Show("Please enter password to unlock!");
            e.Cancel = true;
        }
    }

    private void btnLock_Click(object sender, EventArgs e)
    {
        LockOrUnlock();
    }

    private void LockOrUnlock()
    {
        var password = txtPassword.Text.Trim();

        var isLock = btnLock.Text == "Lock";

        if (isLock)
        {
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter the lock password!");
                return;
            }

            DataStore.LockPassword = password;

            txtPassword.Text = "";

            btnLock.Text = "Unlock";
            btnExit.Text = "Exit";
            lblMessage.Visible = true;
            isLocked = true;
        }
        else
        {
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter the lock password!");
                return;
            }

            if (password != DataStore.LockPassword)
            {
                MessageBox.Show("The lock password is incorrect!");
                return;
            }

            DataStore.LockPassword = null;

            confirmClose = true;

            Close();
        }
    }

    private void btnExit_Click(object sender, EventArgs e)
    {
        if (!isLocked)
        {
            Close();
            return;
        }

        var result = MessageBox.Show("Are you sure to exit the application?", "Confirm", MessageBoxButtons.YesNo);

        if (result == DialogResult.Yes)
        {
            confirmClose = true;

            Application.Exit();
        }
    }

    private void txtPassword_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) LockOrUnlock();
    }
}