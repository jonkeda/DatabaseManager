using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Manager.Model.TableDesigner;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Enum;
using Databases.Model.Schema;

namespace DatabaseManager.Controls;

public partial class UC_TableConstraints : UserControl
{
    public GeneateChangeScriptsHandler OnGenerateChangeScripts;

    public UC_TableConstraints()
    {
        InitializeComponent();
    }

    public bool Inited { get; private set; }

    public bool LoadedData { get; private set; }

    public Table Table { get; set; }

    public DatabaseType DatabaseType { get; set; }
    public event ColumnSelectHandler OnColumnSelect;

    private void UC_TableConstraints_Load(object sender, EventArgs e)
    {
        if (DatabaseType == DatabaseType.MySql) dgvConstraints.Columns["colComment"].Visible = false;
    }

    public void InitControls()
    {
        if (DatabaseType == DatabaseType.Oracle || DatabaseType == DatabaseType.MySql ||
            DatabaseType == DatabaseType.Sqlite) colComment.Visible = false;

        if (DatabaseType == DatabaseType.Sqlite) colColumnName.Visible = true;

        Inited = true;
    }

    public void LoadConstraints(IEnumerable<TableConstraintDesignerInfo> constraintDesignerInfos)
    {
        dgvConstraints.Rows.Clear();

        foreach (var constriant in constraintDesignerInfos)
        {
            var rowIndex = dgvConstraints.Rows.Add();

            var row = dgvConstraints.Rows[rowIndex];

            row.Cells[colColumnName.Name].Value = constriant.ColumnName;
            row.Cells[colName.Name].Value = constriant.Name;
            row.Cells[colDefinition.Name].Value = constriant.Definition;
            row.Cells[colComment.Name].Value = constriant.Comment;

            row.Tag = constriant;
        }

        LoadedData = true;

        AutoSizeColumns();
        dgvConstraints.ClearSelection();
    }

    private void AutoSizeColumns()
    {
        DataGridViewHelper.AutoSizeLastColumn(dgvConstraints);
    }

    public List<TableConstraintDesignerInfo> GetConstraints()
    {
        var constraintDesingerInfos = new List<TableConstraintDesignerInfo>();

        foreach (DataGridViewRow row in dgvConstraints.Rows)
        {
            var constraint = new TableConstraintDesignerInfo();

            var constraintName = row.Cells[colName.Name].Value?.ToString();
            var columnName = row.Cells[colColumnName.Name].Value?.ToString();

            if (!string.IsNullOrEmpty(constraintName) || !string.IsNullOrEmpty(columnName))
            {
                var tag = row.Tag as TableConstraintDesignerInfo;

                constraint.OldName = tag?.OldName;
                constraint.Name = constraintName;
                constraint.ColumnName = columnName;
                constraint.Definition = DataGridViewHelper.GetCellStringValue(row, colDefinition.Name);
                constraint.Comment = DataGridViewHelper.GetCellStringValue(row, colComment.Name);

                row.Tag = constraint;

                constraintDesingerInfos.Add(constraint);
            }
        }

        return constraintDesingerInfos;
    }

    private void dgvConstraints_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) DeleteRow();
    }

    private void DeleteRow()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvConstraints);

        if (row != null && !row.IsNewRow) dgvConstraints.Rows.RemoveAt(row.Index);
    }

    private void tsmiDeleteConstraint_Click(object sender, EventArgs e)
    {
        DeleteRow();
    }

    private void dgvConstraints_SizeChanged(object sender, EventArgs e)
    {
        AutoSizeColumns();
    }

    private void dgvConstraints_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    public void OnSaved()
    {
        for (var i = 0; i < dgvConstraints.RowCount; i++)
        {
            var row = dgvConstraints.Rows[i];

            var keyDesingerInfo = row.Tag as TableForeignKeyDesignerInfo;

            if (keyDesingerInfo != null && !string.IsNullOrEmpty(keyDesingerInfo.Name))
                keyDesingerInfo.OldName = keyDesingerInfo.Name;
        }
    }

    public void EndEdit()
    {
        dgvConstraints.EndEdit();
        dgvConstraints.CurrentCell = null;
    }

    private void dgvConstraints_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var row = DataGridViewHelper.GetSelectedRow(dgvConstraints);

            if (row != null)
            {
                var isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                tsmiDeleteConstraint.Enabled = !isEmptyNewRow;
            }
            else
            {
                tsmiDeleteConstraint.Enabled = false;
            }

            contextMenuStrip1.Show(dgvConstraints, e.Location);
        }
    }

    private void dgvConstraints_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        dgvConstraints.EndEdit();
        dgvConstraints.CurrentCell = null;
        dgvConstraints.Rows[e.RowIndex].Selected = true;
    }

    private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
    {
        OnGenerateChangeScripts?.Invoke();
    }

    private void dgvConstraints_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colColumnName.Index)
        {
            var row = dgvConstraints.Rows[e.RowIndex];
            var cell = row.Cells[colColumnName.Name];

            var designerInfo = row.Tag as TableConstraintDesignerInfo;

            OnColumnSelect?.Invoke(DatabaseObjectType.Constraint,
                designerInfo?.ColumnName == null
                    ? Enumerable.Empty<SimpleColumn>()
                    : new SimpleColumn[] { new() { ColumnName = designerInfo.ColumnName } },
                false, true
            );
        }
    }

    public void SetRowColumns(IEnumerable<SimpleColumn> columnInfos)
    {
        var cell = dgvConstraints.CurrentCell;

        if (cell != null)
        {
            var columnName = columnInfos.FirstOrDefault()?.ColumnName;

            cell.Value = columnName;

            var designerInfo = cell.OwningRow.Tag as TableConstraintDesignerInfo;

            if (designerInfo == null) designerInfo = new TableConstraintDesignerInfo();

            designerInfo.ColumnName = columnName;

            cell.OwningRow.Tag = designerInfo;
        }
    }
}