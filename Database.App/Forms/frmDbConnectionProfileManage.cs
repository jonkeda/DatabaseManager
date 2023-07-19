using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseManager.Profile;

namespace DatabaseManager.Forms;

public partial class frmDbConnectionProfileManage : Form
{
    private readonly string accountId;

    public frmDbConnectionProfileManage()
    {
        InitializeComponent();
    }

    public frmDbConnectionProfileManage(string accountId)
    {
        InitializeComponent();

        this.accountId = accountId;
    }

    public DatabaseType DatabaseType { get; set; }

    private void frmDbConnectionProfile_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        dgvDbConnectionProfile.AutoGenerateColumns = false;

        LoadProfiles();
    }

    private async void LoadProfiles()
    {
        dgvDbConnectionProfile.Rows.Clear();

        var profiles = await ConnectionProfileManager.GetProfilesByAccountId(accountId);

        foreach (var profile in profiles)
            dgvDbConnectionProfile.Rows.Add(profile.Id, profile.Name, profile.Server, profile.Port, profile.Database);

        dgvDbConnectionProfile.Tag = profiles;
    }

    private async void btnDelete_Click(object sender, EventArgs e)
    {
        var count = dgvDbConnectionProfile.SelectedRows.Count;

        if (count == 0)
        {
            MessageBox.Show("No any row selected.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete the selected profiles?", "Confirm", MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            var ids = new List<string>();
            var rowIndexes = new List<int>();

            for (var i = count - 1; i >= 0; i--)
            {
                var rowIndex = dgvDbConnectionProfile.SelectedRows[i].Index;

                ids.Add(dgvDbConnectionProfile.Rows[rowIndex].Cells[colId.Name].Value.ToString());

                rowIndexes.Add(rowIndex);
            }

            var success = await DeleteConnections(ids);

            if (success) rowIndexes.ForEach(item => { dgvDbConnectionProfile.Rows.RemoveAt(item); });
        }
    }

    private async Task<bool> DeleteConnections(List<string> ids)
    {
        return await ConnectionProfileManager.Delete(ids);
    }

    private async void btnClear_Click(object sender, EventArgs e)
    {
        var count = dgvDbConnectionProfile.Rows.Count;

        if (count == 0)
        {
            MessageBox.Show("No record.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete all profiles of this account?", "Confirm",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var ids = new List<string>();

            for (var i = 0; i < dgvDbConnectionProfile.Rows.Count; i++)
                ids.Add(dgvDbConnectionProfile.Rows[i].Cells[colId.Name].Value.ToString());

            var success = await DeleteConnections(ids);

            if (success) dgvDbConnectionProfile.Rows.Clear();
        }
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}