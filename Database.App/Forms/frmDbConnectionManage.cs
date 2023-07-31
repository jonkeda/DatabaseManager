using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Profile;
using Databases.Interpreter.Helper;
using Databases.Manager.Helper;
using Databases.Model.Enum;

namespace DatabaseManager;

public partial class frmDbConnectionManage : Form
{
    private readonly string actionButtonText = "Manage";

    public frmDbConnectionManage()
    {
        InitializeComponent();
    }

    public frmDbConnectionManage(DatabaseType databaseType)
    {
        InitializeComponent();
        DatabaseType = databaseType;
    }

    public bool IsForSelecting { get; set; }
    public DatabaseType DatabaseType { get; set; }

    public AccountProfileInfo SelectedAccountProfileInfo { get; private set; }
    public FileConnectionProfileInfo SelectedFileConnectionProfileInfo { get; private set; }

    private void frmDbConnectionManage_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        dgvDbConnection.AutoGenerateColumns = false;

        var middleCenter = DataGridViewContentAlignment.MiddleCenter;

        colIntegratedSecurity.HeaderCell.Style.Alignment = middleCenter;
        colProfiles.HeaderCell.Style.Alignment = middleCenter;
        colDatabaseVisibility.HeaderCell.Style.Alignment = middleCenter;

        btnSelect.Visible = IsForSelecting;
        panelDbType.Visible = !IsForSelecting;
        panelOperation.Visible = !IsForSelecting;

        if (IsForSelecting)
        {
            Text = "Select Connection";
            dgvDbConnection.MultiSelect = false;
            dgvDbConnection.Top = panelDbType.Top;
            dgvDbConnection.Height += panelDbType.Height;

            colProfiles.Visible = false;
            colDatabaseVisibility.Visible = false;
            dgvDbConnection.AutoResizeColumnHeadersHeight();
            Width -= colProfiles.Width + colDatabaseVisibility.Width - 10;
        }

        LoadDbTypes();
    }

    public void LoadDbTypes()
    {
        var databaseTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

        foreach (var value in databaseTypes) cboDbType.Items.Add(value.ToString());

        if (cboDbType.Items.Count > 0)
        {
            if (!IsForSelecting)
                cboDbType.SelectedIndex = 0;
            else
                cboDbType.Text = DatabaseType.ToString();
        }
    }

    private void btnAdd_Click(object sender, EventArgs e)
    {
        var databaseType = cboDbType.Text;

        if (string.IsNullOrEmpty(databaseType))
        {
            MessageBox.Show("Please select a database type first.");
        }
        else
        {
            var dbType = ManagerUtil.GetDatabaseType(databaseType);

            var isFileConnection = IsFileConnection();

            if (!isFileConnection)
            {
                var form = new frmAccountInfo(dbType);
                var result = form.ShowDialog();

                if (result == DialogResult.OK) LoadAccounts();
            }
            else
            {
                var form = new frmFileConnection(dbType);
                var result = form.ShowDialog();

                if (result == DialogResult.OK) LoadFileConnections();
            }
        }
    }

    private async void btnDelete_Click(object sender, EventArgs e)
    {
        var count = dgvDbConnection.SelectedRows.Count;

        if (count == 0)
        {
            MessageBox.Show("No any row selected.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete selected records with their profiles?", "Confirm",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var ids = new List<string>();
            var rowIndexes = new List<int>();

            for (var i = count - 1; i >= 0; i--)
            {
                var rowIndex = dgvDbConnection.SelectedRows[i].Index;

                ids.Add(dgvDbConnection.Rows[rowIndex].Cells[colId.Name].Value.ToString());

                rowIndexes.Add(rowIndex);
            }

            var success = await DeleteConnections(ids);

            if (success) rowIndexes.ForEach(item => { dgvDbConnection.Rows.RemoveAt(item); });
        }
    }

    private async void btnClear_Click(object sender, EventArgs e)
    {
        var count = dgvDbConnection.Rows.Count;

        if (count == 0)
        {
            MessageBox.Show("No record.");
            return;
        }

        if (MessageBox.Show("Are you sure to delete all records with their profiles?", "Confirm",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var ids = new List<string>();

            for (var i = 0; i < dgvDbConnection.Rows.Count; i++)
                ids.Add(dgvDbConnection.Rows[i].Cells[colId.Name].Value.ToString());

            var success = await DeleteConnections(ids);

            if (success) dgvDbConnection.Rows.Clear();
        }
    }

    private async Task<bool> DeleteConnections(List<string> ids)
    {
        if (!IsFileConnection())
            return await AccountProfileManager.Delete(ids);
        return await FileConnectionProfileManager.Delete(ids);
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void cboDbType_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cboDbType.SelectedIndex >= 0)
        {
            var isFileConnection = IsFileConnection();

            SetColumnsVisible(isFileConnection);

            if (!isFileConnection)
                LoadAccounts();
            else
                LoadFileConnections();

            dgvDbConnection.ClearSelection();
        }
    }

    private void SetColumnsVisible(bool isFileConnection)
    {
        colServer.Visible = !isFileConnection;
        colPort.Visible = !isFileConnection;
        colIntegratedSecurity.Visible = !isFileConnection;
        colUserName.Visible = !isFileConnection;
        colName.Visible = isFileConnection;
        colDatabase.Visible = isFileConnection;
        colProfiles.Visible = !IsForSelecting && !isFileConnection;

        var dbType = ManagerUtil.GetDatabaseType(cboDbType.Text);

        colDatabaseVisibility.Visible =
            !IsForSelecting && !(dbType == DatabaseType.Oracle || dbType == DatabaseType.Sqlite);
    }

    private bool IsFileConnection()
    {
        var dbType = cboDbType.Text;

        if (!string.IsNullOrEmpty(dbType)) return ManagerUtil.IsFileConnection(ManagerUtil.GetDatabaseType(dbType));

        return false;
    }

    private async void LoadAccounts()
    {
        dgvDbConnection.Rows.Clear();

        var type = cboDbType.Text;

        var profiles = await AccountProfileManager.GetProfiles(type);

        foreach (var profile in profiles)
            dgvDbConnection.Rows.Add(profile.Id, profile.Server, profile.Port, profile.IntegratedSecurity,
                profile.UserId, null, null, actionButtonText, actionButtonText);

        dgvDbConnection.Tag = profiles;
    }

    private async void LoadFileConnections()
    {
        dgvDbConnection.Rows.Clear();

        var type = cboDbType.Text;

        var profiles = await FileConnectionProfileManager.GetProfiles(type);

        foreach (var profile in profiles)
            dgvDbConnection.Rows.Add(profile.Id, null, null, null, null, profile.Name, profile.Database,
                actionButtonText, actionButtonText);

        dgvDbConnection.Tag = profiles;
    }

    private void btnEdit_Click(object sender, EventArgs e)
    {
        Edit();
    }

    private void Edit()
    {
        var isFileConnection = IsFileConnection();

        if (!isFileConnection)
        {
            var profile = GetSelectedAccountProfile();

            if (profile != null)
            {
                var form = new frmAccountInfo(ManagerUtil.GetDatabaseType(cboDbType.Text), true)
                    { AccountProfileInfo = profile };

                if (form.ShowDialog() == DialogResult.OK) LoadAccounts();
            }
        }
        else
        {
            var profile = GetSelectedFileConnectionProfile();

            if (profile != null)
            {
                var frmAccountInfo = new frmFileConnection(ManagerUtil.GetDatabaseType(cboDbType.Text), true)
                    { FileConnectionProfileInfo = profile };

                if (frmAccountInfo.ShowDialog() == DialogResult.OK) LoadFileConnections();
            }
        }
    }

    private string GetSelectedId()
    {
        var count = dgvDbConnection.SelectedRows.Count;

        if (count == 0)
        {
            MessageBox.Show("Please select row by clicking row header.");
            return null;
        }

        var id = dgvDbConnection.SelectedRows[0].Cells[colId.Name].Value.ToString();

        return id;
    }

    private AccountProfileInfo GetSelectedAccountProfile()
    {
        var id = GetSelectedId();

        if (string.IsNullOrEmpty(id)) return null;

        var profiles = dgvDbConnection.Tag as IEnumerable<AccountProfileInfo>;

        var profile = profiles.FirstOrDefault(item => item.Id == id);

        return profile;
    }

    private FileConnectionProfileInfo GetSelectedFileConnectionProfile()
    {
        var id = GetSelectedId();

        if (string.IsNullOrEmpty(id)) return null;

        var profiles = dgvDbConnection.Tag as IEnumerable<FileConnectionProfileInfo>;

        var profile = profiles.FirstOrDefault(item => item.Id == id);

        return profile;
    }

    private void SelectRecord()
    {
        var isFileConnection = IsFileConnection();

        var selected = false;

        if (!isFileConnection)
        {
            var profile = GetSelectedAccountProfile();

            if (profile != null)
            {
                SelectedAccountProfileInfo = profile;

                selected = true;
            }
        }
        else
        {
            var profile = GetSelectedFileConnectionProfile();

            if (profile != null)
            {
                SelectedFileConnectionProfileInfo = profile;

                selected = true;
            }
        }

        if (selected)
        {
            DialogResult = DialogResult.OK;

            Close();
        }
    }

    private void btnSelect_Click(object sender, EventArgs e)
    {
        SelectRecord();
    }

    private void dgvDbConnection_DoubleClick(object sender, EventArgs e)
    {
        if (IsForSelecting)
            SelectRecord();
        else
            Edit();
    }

    private void dgvDbConnection_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        dgvDbConnection.ClearSelection();
    }

    private void dgvDbConnection_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = DataGridViewHelper.GetCurrentRow(dgvDbConnection);

        if (row == null) return;

        var id = row.Cells[colId.Name].Value.ToString();

        if (e.ColumnIndex == colProfiles.Index)
        {
            var form = new frmDbConnectionProfileManage(id);

            form.ShowDialog();
        }
        else if (e.ColumnIndex == colDatabaseVisibility.Index)
        {
            var form = new frmDatabaseVisibility(id);

            var profile =
                (dgvDbConnection.Tag as IEnumerable<AccountProfileInfo>).FirstOrDefault(item => item.Id == id);

            form.AccountProfileInfo = profile;
            form.DatabaseType = ManagerUtil.GetDatabaseType(cboDbType.Text);

            form.ShowDialog();
        }
    }
}