using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager;

public partial class frmColumSelect : Form
{
    private bool isIndexColumn;

    public frmColumSelect()
    {
        InitializeComponent();
    }

    public bool ColumnIsReadOnly { get; set; }
    public bool IsSingleSelect { get; set; }
    public List<SimpleColumn> SelectedColumns { get; private set; }

    private void frmColumSelect_Load(object sender, EventArgs e)
    {
        InitGrid();
    }

    private void InitGrid()
    {
        if (ColumnIsReadOnly)
        {
            colColumName.ReadOnly = true;
            dgvColumns.AllowUserToAddRows = false;
        }

        foreach (DataGridViewRow row in dgvColumns.Rows)
            if (row.Tag == null)
                row.Tag = new TableColumnDesingerInfo();
    }

    public void InitControls(IEnumerable<SimpleColumn> columns, bool showSortColumn = true)
    {
        isIndexColumn = columns?.FirstOrDefault()?.GetType() == typeof(IndexColumn);

        colSort.DataSource = Enum.GetValues(typeof(SortType));
        colColumName.DataSource = columns.ToList();
        colColumName.DisplayMember = nameof(SimpleColumn.ColumnName);
        colColumName.ValueMember = nameof(SimpleColumn.ColumnName);

        if (!showSortColumn)
        {
            colSort.Visible = false;
            colColumName.Width = dgvColumns.Width - dgvColumns.RowHeadersWidth;
        }
    }

    public void LoadColumns(IEnumerable<SimpleColumn> columns)
    {
        dgvColumns.Rows.Clear();

        foreach (var column in columns)
        {
            var rowIndex = dgvColumns.Rows.Add();

            var row = dgvColumns.Rows[rowIndex];

            row.Cells[colColumName.Name].Value = column.ColumnName;

            if (isIndexColumn)
                row.Cells[colSort.Name].Value =
                    (column as IndexColumn).IsDesc ? SortType.Descending : SortType.Ascending;
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        var columns = new List<SimpleColumn>();

        var order = 1;
        foreach (DataGridViewRow row in dgvColumns.Rows)
            if (!row.IsNewRow)
            {
                SimpleColumn columnInfo = null;

                if (isIndexColumn)
                {
                    columnInfo = new IndexColumn();
                    (columnInfo as IndexColumn).IsDesc =
                        row.Cells[colSort.Name].Value?.ToString() == SortType.Descending.ToString();
                }
                else
                {
                    columnInfo = new SimpleColumn();
                }

                columnInfo.Order = order;
                columnInfo.ColumnName = row.Cells[colColumName.Name].Value?.ToString();

                columns.Add(columnInfo);

                order++;
            }

        if (columns.Count == 0)
        {
            MessageBox.Show("Please select column(s).");
            return;
        }

        if (columns.Count > 1)
        {
            if (IsSingleSelect)
            {
                MessageBox.Show("Only allow select one column.");
                return;
            }
        }

        SelectedColumns = columns;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void tsmiDeleteColumn_Click(object sender, EventArgs e)
    {
        DeleteRow();
    }

    private void dgvColumns_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var row = DataGridViewHelper.GetSelectedRow(dgvColumns);

            var isNewRow = row != null && row.IsNewRow;

            tsmiDeleteColumn.Enabled = !isNewRow;

            contextMenuStrip1.Show(dgvColumns, e.Location);
        }
    }

    private void DeleteRow()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvColumns);

        if (row != null && !row.IsNewRow) dgvColumns.Rows.RemoveAt(row.Index);
    }

    private void dgvColumns_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) DeleteRow();
    }

    private void dgvColumns_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        dgvColumns.EndEdit();
        dgvColumns.CurrentCell = null;
        dgvColumns.Rows[e.RowIndex].Selected = true;
    }

    private void dgvColumns_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvColumns_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        dgvColumns.ClearSelection();
    }
}