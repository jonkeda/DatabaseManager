using System;
using System.Data;
using System.Windows.Forms;
using DatabaseManager.Model;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.DbObjectDisplay;
using Databases.Manager.Model.Diagnose;
using Databases.Model.Connection;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Enum;

namespace DatabaseManager;

public partial class frmTableDiagnoseResult : Form
{
    public frmTableDiagnoseResult()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public ConnectionInfo ConnectionInfo { get; set; }

    private void frmTableDiagnoseResult_Load(object sender, EventArgs e)
    {
        dgvResult.ClearSelection();
    }

    public void LoadResult(TableDiagnoseResult result)
    {
        foreach (var item in result.Details)
        {
            var rowIndex = dgvResult.Rows.Add();

            var row = dgvResult.Rows[rowIndex];

            row.Cells[colTableName.Name].Value = GetTableName(item.DatabaseObject);
            row.Cells[colObjectType.Name].Value = item.DatabaseObject.GetType().Name;
            row.Cells[colObjectName.Name].Value = item.DatabaseObject.Name;
            row.Cells[colInvalidRecordCount.Name].Value = item.RecordCount;

            row.Tag = item;
        }
    }

    private string GetTableName(DatabaseObject dbObject)
    {
        if (dbObject is TableChild tableChild)
            return tableChild.TableName;
        if (dbObject is Table table) return table.Name;

        return string.Empty;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void tsmiCopy_Click(object sender, EventArgs e)
    {
        Copy(DataGridViewClipboardCopyMode.EnableWithoutHeaderText);
    }

    private void tsmiCopyWithHeader_Click(object sender, EventArgs e)
    {
        Copy(DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText);
    }

    private void Copy(DataGridViewClipboardCopyMode mode)
    {
        dgvResult.ClipboardCopyMode = mode;

        Clipboard.SetDataObject(dgvResult.GetClipboardContent());
    }

    private void dgvResult_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var canCopy = dgvResult.GetCellCount(DataGridViewElementStates.Selected) > 0;

            tsmiCopy.Enabled = canCopy;
            tsmiCopyWithHeader.Enabled = canCopy;

            contextMenuStrip1.Show(dgvResult, e.Location);
        }
    }

    private void dgvResult_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        dgvResult.ClearSelection();
    }

    private void dgvResult_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
    {
        dgvResult.ClearSelection();
    }

    private void dgvResult_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colInvalidRecordCount.Index)
        {
            var resultItem = dgvResult.Rows[e.RowIndex].Tag as TableDiagnoseResultDetail;

            var sql = resultItem.Sql;

            var form = new frmSqlQuery { ReadOnly = true, ShowEditorMessage = false, SplitterDistance = 80 };

            form.Init();

            var displayInfo = new DatabaseObjectDisplayInfo
            {
                DatabaseType = DatabaseType,
                ConnectionInfo = ConnectionInfo,
                Content = sql
            };

            form.Query(displayInfo);

            form.ShowDialog();
        }
    }

    private void tsmiSave_Click(object sender, EventArgs e)
    {
        Save();
    }

    private void Save()
    {
        if (dgvResult == null) dlgSave = new SaveFileDialog();

        dlgSave.FileName = "";

        var result = dlgSave.ShowDialog();

        if (result == DialogResult.OK)
        {
            var table = new DataTable();

            foreach (DataGridViewColumn column in dgvResult.Columns)
                table.Columns.Add(new DataColumn { ColumnName = column.HeaderText });

            foreach (DataGridViewRow row in dgvResult.Rows)
            {
                var r = table.Rows.Add();

                foreach (DataGridViewCell cell in row.Cells) r[cell.ColumnIndex] = cell.Value;
            }

            DataTableHelper.WriteToFile(table, dlgSave.FileName);
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        Save();
    }
}