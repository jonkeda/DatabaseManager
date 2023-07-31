using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Interpreter;
using Databases.Manager.Helper;
using Databases.Manager.Manager;
using Databases.Manager.Model.TableDesigner;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;
using Databases.Model.Schema;

namespace DatabaseManager.Controls;

public partial class UC_TableIndexes : UserControl
{
    private DateTime dtTypeCellClick = DateTime.Now;
    public GeneateChangeScriptsHandler OnGenerateChangeScripts;

    public UC_TableIndexes()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }
    public Table Table { get; set; }

    public bool Inited { get; private set; }

    public bool LoadedData { get; private set; }

    public event ColumnSelectHandler OnColumnSelect;

    public void InitControls(DbInterpreter dbInterpreter)
    {
        if (Inited) return;

        if (!ManagerUtil.SupportComment(DatabaseType)) colComment.Visible = false;

        var types = Enum.GetValues(typeof(IndexType));

        var typeNames = new List<string>();

        foreach (var type in types)
            if (dbInterpreter.IndexType.HasFlag((IndexType)type) && (IndexType)type != IndexType.None)
                typeNames.Add(type.ToString());

        colType.DataSource = typeNames;

        if (DatabaseType == DatabaseType.Oracle) colComment.Visible = false;

        Inited = true;
    }

    public void LoadIndexes(IEnumerable<TableIndexDesignerInfo> indexDesignerInfos)
    {
        dgvIndexes.Rows.Clear();

        foreach (var index in indexDesignerInfos)
        {
            var rowIndex = dgvIndexes.Rows.Add();

            var row = dgvIndexes.Rows[rowIndex];

            row.Cells[colIndexName.Name].Value = index.Name;
            row.Cells[colType.Name].Value = GetIndexTypeEnumName(index.Type);
            row.Cells[colColumns.Name].Value = GetColumnsDisplayText(index.Columns);
            row.Cells[colComment.Name].Value = index.Comment;

            row.Tag = index;
        }

        LoadedData = true;

        AutoSizeColumns();
        dgvIndexes.ClearSelection();
    }

    private string GetIndexTypeEnumName(string type)
    {
        var typeNames = Enum.GetNames(typeof(IndexType));

        foreach (var tn in typeNames)
            if (tn.ToLower() == type.ToLower())
                return tn;

        return type;
    }

    public void LoadPrimaryKeys(IEnumerable<TableColumnDesingerInfo> columnDesingerInfos)
    {
        var primaryRowIndex = -1;

        foreach (DataGridViewRow row in dgvIndexes.Rows)
            if (!row.IsNewRow)
            {
                var indexDesignerInfo = row.Tag as TableIndexDesignerInfo;

                if (indexDesignerInfo != null && indexDesignerInfo.IsPrimary)
                {
                    primaryRowIndex = row.Index;

                    if (columnDesingerInfos.Count() > 0)
                    {
                        indexDesignerInfo.Columns.Clear();

                        indexDesignerInfo.Columns.AddRange(
                            columnDesingerInfos.Select(item => new IndexColumn { ColumnName = item.Name }));

                        row.Cells[colColumns.Name].Value = GetColumnsDisplayText(indexDesignerInfo.Columns);
                    }

                    break;
                }
            }

        if (primaryRowIndex >= 0 && !columnDesingerInfos.Any())
        {
            dgvIndexes.Rows.RemoveAt(primaryRowIndex);
        }
        else if (primaryRowIndex < 0 && columnDesingerInfos.Count() > 0)
        {
            var rowIndex = dgvIndexes.Rows.Add();

            var tableIndexDesignerInfo = new TableIndexDesignerInfo
            {
                Type = DatabaseType == DatabaseType.Oracle ? IndexType.Unique.ToString() : IndexType.Primary.ToString(),
                Name = IndexManager.GetPrimaryKeyDefaultName(Table),
                IsPrimary = true
            };

            tableIndexDesignerInfo.Columns.AddRange(columnDesingerInfos.Select(item =>
                new IndexColumn { ColumnName = item.Name }));

            var primaryRow = dgvIndexes.Rows[rowIndex];
            primaryRow.Cells[colType.Name].Value = tableIndexDesignerInfo.Type;
            primaryRow.Cells[colIndexName.Name].Value = tableIndexDesignerInfo.Name;
            primaryRow.Cells[colColumns.Name].Value = GetColumnsDisplayText(tableIndexDesignerInfo.Columns);

            tableIndexDesignerInfo.ExtraPropertyInfo = new TableIndexExtraPropertyInfo { Clustered = true };

            primaryRow.Tag = tableIndexDesignerInfo;
        }

        ShowIndexExtraPropertites();
    }

    private string GetColumnsDisplayText(IEnumerable<IndexColumn> columns)
    {
        return string.Join(",", columns.Select(item => item.ColumnName));
    }

    public List<TableIndexDesignerInfo> GetIndexes()
    {
        var indexDesingerInfos = new List<TableIndexDesignerInfo>();

        foreach (DataGridViewRow row in dgvIndexes.Rows)
        {
            var index = new TableIndexDesignerInfo();

            var indexName = row.Cells[colIndexName.Name].Value?.ToString();

            if (!string.IsNullOrEmpty(indexName))
            {
                var tag = row.Tag as TableIndexDesignerInfo;

                index.OldName = tag?.OldName;
                index.OldType = tag?.OldType;
                index.Name = indexName;
                index.Type = DataGridViewHelper.GetCellStringValue(row, colType.Name);
                index.Columns = tag?.Columns;
                index.Comment = DataGridViewHelper.GetCellStringValue(row, colComment.Name);
                index.ExtraPropertyInfo = tag?.ExtraPropertyInfo;
                index.IsPrimary = tag?.IsPrimary == true;
                row.Tag = index;

                indexDesingerInfos.Add(index);
            }
        }

        return indexDesingerInfos;
    }

    private void dgvIndexes_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) DeleteRow();
    }

    private void DeleteRow()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvIndexes);

        if (row != null && !row.IsNewRow) dgvIndexes.Rows.RemoveAt(row.Index);
    }

    private void tsmiDeleteIndex_Click(object sender, EventArgs e)
    {
        DeleteRow();
    }

    private void dgvIndexes_SizeChanged(object sender, EventArgs e)
    {
        AutoSizeColumns();
    }

    private void AutoSizeColumns()
    {
        DataGridViewHelper.AutoSizeLastColumn(dgvIndexes);
    }

    private void dgvIndexes_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvIndexes_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var row = DataGridViewHelper.GetSelectedRow(dgvIndexes);

            if (row != null)
            {
                var isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                tsmiDeleteIndex.Enabled = !isEmptyNewRow;
            }
            else
            {
                tsmiDeleteIndex.Enabled = false;
            }

            contextMenuStrip1.Show(dgvIndexes, e.Location);
        }
    }

    private void dgvIndexes_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        dgvIndexes.EndEdit();
        dgvIndexes.CurrentCell = null;
        dgvIndexes.Rows[e.RowIndex].Selected = true;
    }

    private void dgvIndexes_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colType.Index)
        {
            ShowIndexExtraPropertites();

            var row = dgvIndexes.Rows[e.RowIndex];

            var nameCell = row.Cells[colIndexName.Name];

            var type = DataGridViewHelper.GetCellStringValue(row, colType.Name);

            var indexName = DataGridViewHelper.GetCellStringValue(nameCell);

            if (string.IsNullOrEmpty(indexName))
                nameCell.Value = type == IndexType.Primary.ToString()
                    ? IndexManager.GetPrimaryKeyDefaultName(Table)
                    : IndexManager.GetIndexDefaultName(type, Table);

            if (row.Tag != null) (row.Tag as TableIndexDesignerInfo).IsPrimary = type == IndexType.Primary.ToString();
        }
    }

    private void dgvIndexes_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colColumns.Index)
        {
            var row = dgvIndexes.Rows[e.RowIndex];
            var cell = row.Cells[colColumns.Name];

            var indexName = DataGridViewHelper.GetCellStringValue(row, colIndexName.Name);
            var type = DataGridViewHelper.GetCellStringValue(row, colType.Name);

            if (!string.IsNullOrEmpty(indexName))
            {
                var indexDesignerInfo = row.Tag as TableIndexDesignerInfo;

                OnColumnSelect?.Invoke(DatabaseObjectType.Index,
                    indexDesignerInfo?.Columns == null
                        ? Enumerable.Empty<IndexColumn>()
                        : indexDesignerInfo?.Columns,
                    type == IndexType.Primary.ToString()
                );
            }
        }
    }

    public void SetRowColumns(IEnumerable<IndexColumn> columnInfos)
    {
        var cell = dgvIndexes.CurrentCell;

        if (cell != null)
        {
            cell.Value = string.Join(",", columnInfos.Select(item => item.ColumnName));

            var tableIndexDesignerInfo = cell.OwningRow.Tag as TableIndexDesignerInfo;

            if (tableIndexDesignerInfo == null) tableIndexDesignerInfo = new TableIndexDesignerInfo();

            tableIndexDesignerInfo.Columns = columnInfos.ToList();

            cell.OwningRow.Tag = tableIndexDesignerInfo;
        }
    }

    private void dgvIndexes_UserAddedRow(object sender, DataGridViewRowEventArgs e)
    {
        if (e.Row != null)
            if (e.Row.Tag == null)
                e.Row.Tag = new TableIndexDesignerInfo();
    }

    private void ShowIndexExtraPropertites()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvIndexes);

        if (row != null)
        {
            var indexName = DataGridViewHelper.GetCellStringValue(row, colIndexName.Name);

            if (string.IsNullOrEmpty(indexName)) return;

            var index = row.Tag as TableIndexDesignerInfo;

            if (index == null)
            {
                index = new TableIndexDesignerInfo();
                row.Tag = index;
            }

            var extralProperty = index?.ExtraPropertyInfo;

            var typeCell = row.Cells[colType.Name];

            if (extralProperty == null)
            {
                extralProperty = new TableIndexExtraPropertyInfo();
                index.ExtraPropertyInfo = extralProperty;

                if (DataGridViewHelper.GetCellStringValue(typeCell) != IndexType.Primary.ToString())
                    index.ExtraPropertyInfo.Clustered = false;
            }

            if (DatabaseType == DatabaseType.Oracle)
                indexPropertites.HiddenProperties = new[] { nameof(extralProperty.Clustered) };
            else
                indexPropertites.HiddenProperties = null;

            indexPropertites.SelectedObject = extralProperty;
            indexPropertites.Refresh();
        }
    }

    private void dgvIndexes_SelectionChanged(object sender, EventArgs e)
    {
        ShowIndexExtraPropertites();
    }

    public void EndEdit()
    {
        dgvIndexes.EndEdit();
        dgvIndexes.CurrentCell = null;
    }

    public void OnSaved()
    {
        for (var i = 0; i < dgvIndexes.RowCount; i++)
        {
            var row = dgvIndexes.Rows[i];

            var indexDesingerInfo = row.Tag as TableIndexDesignerInfo;

            if (indexDesingerInfo != null && !string.IsNullOrEmpty(indexDesingerInfo.Name))
            {
                indexDesingerInfo.OldName = indexDesingerInfo.Name;
                indexDesingerInfo.OldType = indexDesingerInfo.Type;
            }
        }
    }

    private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
    {
        OnGenerateChangeScripts?.Invoke();
    }

    private void dgvIndexes_CellLeave(object sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == colType.Index)
        {
            var cell = dgvIndexes.CurrentCell;

            if (cell != null && cell.IsInEditMode && cell.EditedFormattedValue != cell.Value)
                cell.Value = cell.EditedFormattedValue;
        }
    }
}