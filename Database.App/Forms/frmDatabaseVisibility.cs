using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Data;
using DatabaseManager.Model;
using DatabaseManager.Profile;
using Databases.Interpreter.Helper;
using Databases.Interpreter.Utility.Helper;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;
using Databases.Model.Option;

namespace DatabaseManager.Forms;

public partial class frmDatabaseVisibility : Form
{
    private readonly string accountId;

    public frmDatabaseVisibility()
    {
        InitializeComponent();
    }

    public frmDatabaseVisibility(string accountId)
    {
        InitializeComponent();

        this.accountId = accountId;
    }

    public DatabaseType DatabaseType { get; set; }
    public AccountProfileInfo AccountProfileInfo { get; set; }

    private void frmDatabaseVisibility_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        dgvDatabases.AutoGenerateColumns = false;
        colVisible.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        LoadData();
    }

    private async void LoadData()
    {
        dgvDatabases.Rows.Clear();

        var databases = Enumerable.Empty<Database>();

        if (AccountProfileInfo != null)
        {
            if (!AccountProfileInfo.IntegratedSecurity && string.IsNullOrEmpty(AccountProfileInfo.Password))
            {
                var storedInfo = DataStore.GetAccountProfileInfo(AccountProfileInfo.Id);

                if (storedInfo != null && !string.IsNullOrEmpty(storedInfo.Password))
                {
                    AccountProfileInfo.Password = storedInfo.Password;
                }
                else
                {
                    MessageBox.Show("Please specify password for the database.");

                    if (!SetConnectionInfo(AccountProfileInfo)) return;
                }
            }

            var connectionInfo = new ConnectionInfo();

            ObjectHelper.CopyProperties(AccountProfileInfo, connectionInfo);

            databases = await GetDatabases(connectionInfo);
        }

        LoadRecords(databases);
    }

    private async void LoadRecords(IEnumerable<Database> databases)
    {
        var databaseNames = databases.Select(item => item.Name);

        var visibilities = await DatabaseVisibilityManager.GetVisibilities(accountId);

        foreach (var visibility in visibilities)
        {
            var rowIndex = dgvDatabases.Rows.Add(visibility.Id, visibility.Database, visibility.Visible);

            var row = dgvDatabases.Rows[rowIndex];

            if (databaseNames.Count() > 0 &&
                !databaseNames.Any(item => item.ToUpper() == visibility.Database.ToUpper()))
                row.DefaultCellStyle.BackColor = Color.Red;
        }

        foreach (var dbName in databaseNames)
            if (!visibilities.Any(item => item.Database.ToUpper() == dbName.ToUpper()))
                dgvDatabases.Rows.Add(Guid.NewGuid().ToString(), dbName, true);

        dgvDatabases.ClearSelection();
    }

    private async Task<IEnumerable<Database>> GetDatabases(ConnectionInfo connectionInfo)
    {
        var dbInterpreter =
            DbInterpreterHelper.GetDbInterpreter(DatabaseType, connectionInfo, new DbInterpreterOption());

        try
        {
            var databases = await dbInterpreter.GetDatabasesAsync();

            return databases;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));

            return Enumerable.Empty<Database>();
        }
    }

    private bool SetConnectionInfo(AccountProfileInfo accountProfileInfo)
    {
        var frmAccountInfo = new frmAccountInfo(DatabaseType, true) { AccountProfileInfo = accountProfileInfo };

        var dialogResult = frmAccountInfo.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
            var profileInfo = frmAccountInfo.AccountProfileInfo;

            AccountProfileInfo = profileInfo;

            return true;
        }

        return false;
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    private async void btnDelete_Click(object sender, EventArgs e)
    {
        var count = dgvDatabases.SelectedRows.Count;

        if (count == 0)
        {
            MessageBox.Show("No any row selected.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete the selected records?", "Confirm", MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            var ids = new List<string>();
            var rowIndexes = new List<int>();

            for (var i = count - 1; i >= 0; i--)
            {
                var rowIndex = dgvDatabases.SelectedRows[i].Index;

                ids.Add(dgvDatabases.Rows[rowIndex].Cells[colId.Name].Value.ToString());

                rowIndexes.Add(rowIndex);
            }

            var success = await DeleteRecords(ids);

            if (success) rowIndexes.ForEach(item => { dgvDatabases.Rows.RemoveAt(item); });
        }
    }

    private async Task<bool> DeleteRecords(List<string> ids)
    {
        return await DatabaseVisibilityManager.Delete(ids);
    }

    private async void btnClear_Click(object sender, EventArgs e)
    {
        var count = dgvDatabases.Rows.Count;

        if (count == 0)
        {
            MessageBox.Show("No record.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete all records of this account?", "Confirm",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var ids = new List<string>();

            for (var i = 0; i < dgvDatabases.Rows.Count; i++)
                ids.Add(dgvDatabases.Rows[i].Cells[colId.Name].Value.ToString());

            var success = await DeleteRecords(ids);

            if (success) dgvDatabases.Rows.Clear();
        }
    }

    private async void btnVisible_Click(object sender, EventArgs e)
    {
        await SetVisible(true);
    }

    private async void btnInVisible_Click(object sender, EventArgs e)
    {
        await SetVisible(false);
    }

    private async Task<bool> SetVisible(bool visible)
    {
        var count = dgvDatabases.SelectedRows.Count;

        if (count == 0)
        {
            MessageBox.Show("Please select rows first.");
            return false;
        }

        var visibilities = new List<DatabaseVisibilityInfo>();

        foreach (DataGridViewRow row in dgvDatabases.SelectedRows)
        {
            var id = row.Cells[colId.Name].Value.ToString();
            var database = row.Cells[colDatabase.Name].Value.ToString();

            visibilities.Add(new DatabaseVisibilityInfo
                { Id = id, AccountId = accountId, Database = database, Visible = visible });
        }

        var success = await DatabaseVisibilityManager.Save(accountId, visibilities);

        if (success)
        {
            MessageBox.Show("Operate succeeded.");

            LoadData();
        }

        return success;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}