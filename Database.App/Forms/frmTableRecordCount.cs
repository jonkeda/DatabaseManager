using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Model;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.Statistic;

namespace DatabaseManager;

public partial class frmTableRecordCount : Form
{
    public frmTableRecordCount()
    {
        InitializeComponent();
    }

    private void frmTableRecordCount_Load(object sender, EventArgs e)
    {
        dgvResult.ClearSelection();
    }

    public void LoadData(IEnumerable<TableRecordCount> records)
    {
        foreach (var item in records.OrderByDescending(item => item.RecordCount))
        {
            var rowIndex = dgvResult.Rows.Add();

            var row = dgvResult.Rows[rowIndex];

            row.Cells[colTableName.Name].Value = item.TableName;
            row.Cells[colRecordCount.Name].Value = item.RecordCount;

            row.Tag = item;
        }
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