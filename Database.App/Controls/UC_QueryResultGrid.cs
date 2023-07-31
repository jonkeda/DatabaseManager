using System;
using System.Data;
using System.Reflection;
using System.Windows.Forms;
using DatabaseManager.Helper;
using Databases.Interpreter.Utility.Helper;

namespace DatabaseManager.Controls;

public partial class UC_QueryResultGrid : UserControl
{
    public UC_QueryResultGrid()
    {
        InitializeComponent();

        typeof(DataGridView).InvokeMember("DoubleBuffered",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dgvData,
            new object[] { true });
    }

    public void LoadData(DataTable dataTable)
    {
        dgvData.DataSource = DataGridViewHelper.ConvertDataTable(dataTable);
    }

    public void ClearData()
    {
        dgvData.DataSource = null;
    }

    private void tsmiSave_Click(object sender, EventArgs e)
    {
        Save();
    }

    private void Save()
    {
        if (dlgSave == null) dlgSave = new SaveFileDialog();

        dlgSave.FileName = "";

        var result = dlgSave.ShowDialog();
        if (result == DialogResult.OK) DataTableHelper.WriteToFile(dgvData.DataSource as DataTable, dlgSave.FileName);
    }

    private void dgvData_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            SetContextMenuItemVisible();

            contextMenuStrip1.Show(dgvData, e.Location);
        }
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
        dgvData.ClipboardCopyMode = mode;

        Clipboard.SetDataObject(dgvData.GetClipboardContent());
    }

    private void dgvData_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        dgvData.ClearSelection();
    }

    private void dgvData_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        DataGridViewHelper.FormatCell(dgvData, e);
    }

    private void SetContextMenuItemVisible()
    {
        var selectedCount = dgvData.GetCellCount(DataGridViewElementStates.Selected);
        tsmiCopy.Visible = selectedCount > 1;
        tsmiCopyWithHeader.Visible = selectedCount > 1;
        tsmiViewGeometry.Visible = selectedCount == 1 && DataGridViewHelper.IsGeometryValue(dgvData);
        tsmiCopyContent.Visible = selectedCount == 1;
        tsmiShowContent.Visible = selectedCount == 1;
    }

    private void tsmiViewGeometry_Click(object sender, EventArgs e)
    {
        DataGridViewHelper.ShowGeometryViewer(dgvData);
    }

    private void tsmiCopyContent_Click(object sender, EventArgs e)
    {
        var value = DataGridViewHelper.GetCurrentCellValue(dgvData);

        if (!string.IsNullOrEmpty(value)) Clipboard.SetDataObject(value);
    }

    private void tsmiShowContent_Click(object sender, EventArgs e)
    {
        DataGridViewHelper.ShowCellContent(dgvData);
    }
}