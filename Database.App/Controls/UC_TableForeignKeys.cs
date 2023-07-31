using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Manager.Helper;
using Databases.Manager.Manager;
using Databases.Manager.Model.TableDesigner;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;

namespace DatabaseManager.Controls;

public delegate void ColumnMappingSelectHandler(string referenceTableShema, string referenceTableName,
    List<ForeignKeyColumn> mappings);

public partial class UC_TableForeignKeys : UserControl
{
    public GeneateChangeScriptsHandler OnGenerateChangeScripts;
    private readonly List<TableDisplayInfo> tableDisplayInfos = new();

    public UC_TableForeignKeys()
    {
        InitializeComponent();

        colUpdateCascade.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colDeleteCascade.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
    }

    public bool Inited { get; private set; }

    public bool LoadedData { get; private set; }

    public Table Table { get; set; }

    public DatabaseType DatabaseType { get; set; }
    public string DefaultSchema { get; set; }

    public event ColumnMappingSelectHandler OnColumnMappingSelect;

    private void UC_TableForeignKeys_Load(object sender, EventArgs e)
    {
    }

    public void InitControls(IEnumerable<Table> tables)
    {
        if (!ManagerUtil.SupportComment(DatabaseType))
            if (colComment.Visible)
            {
                colComment.Visible = false;
                colKeyName.Width += 100;
                colColumns.Width += 100;
            }

        var sortedTables = new List<Table>();

        foreach (var table in tables.Where(item => item.Schema == DefaultSchema).OrderBy(item => item.Name))
            sortedTables.Add(table);

        foreach (var table in tables.Where(item => item.Schema != DefaultSchema).OrderBy(item => item.Schema)
                     .ThenBy(item => item.Name)) sortedTables.Add(table);

        foreach (var table in sortedTables)
        {
            var tableDisplayInfo = new TableDisplayInfo();

            tableDisplayInfo.Table = table;
            tableDisplayInfo.DisplayName = GetReferenceTableDisplayName(table);

            tableDisplayInfos.Add(tableDisplayInfo);
        }

        colReferenceTable.DataSource = tableDisplayInfos;
        colReferenceTable.ValueMember = nameof(TableDisplayInfo.Table);
        colReferenceTable.DisplayMember = nameof(TableDisplayInfo.DisplayName);

        if (DatabaseType == DatabaseType.Oracle || DatabaseType == DatabaseType.MySql) colComment.Visible = false;

        Inited = true;
    }

    private string GetReferenceTableDisplayName(Table table)
    {
        return table.Schema == DefaultSchema || string.IsNullOrEmpty(table.Schema)
            ? table.Name
            : $"{table.Name}({table.Schema})";
    }

    public void LoadForeignKeys(IEnumerable<TableForeignKeyDesignerInfo> foreignKeyDesignerInfos)
    {
        dgvForeignKeys.Rows.Clear();

        foreach (var key in foreignKeyDesignerInfos)
        {
            var rowIndex = dgvForeignKeys.Rows.Add();

            var row = dgvForeignKeys.Rows[rowIndex];

            row.Cells[colKeyName.Name].Value = key.Name;

            var table = new Table { Schema = key.ReferencedSchema, Name = key.ReferencedTableName };

            var referenceTableName = GetReferenceTableDisplayName(table);

            var tableDisplayInfo = tableDisplayInfos.FirstOrDefault(item => item.DisplayName == referenceTableName);

            row.Cells[colReferenceTable.Name].Value = referenceTableName;
            row.Cells[colReferenceTable.Name].Tag = tableDisplayInfo.Table;

            row.Cells[colColumns.Name].Value = GetColumnMappingsDisplayText(key.Columns);
            row.Cells[colUpdateCascade.Name].Value = key.UpdateCascade;
            row.Cells[colDeleteCascade.Name].Value = key.DeleteCascade;
            row.Cells[colComment.Name].Value = key.Comment;

            row.Tag = key;
        }

        LoadedData = true;

        AutoSizeColumns();
        dgvForeignKeys.ClearSelection();
    }

    public List<TableForeignKeyDesignerInfo> GetForeignKeys()
    {
        var keyDesingerInfos = new List<TableForeignKeyDesignerInfo>();

        foreach (DataGridViewRow row in dgvForeignKeys.Rows)
        {
            var key = new TableForeignKeyDesignerInfo();

            var keyName = row.Cells[colKeyName.Name].Value?.ToString();

            if (!string.IsNullOrEmpty(keyName))
            {
                var tag = row.Tag as TableForeignKeyDesignerInfo;

                var referenceTable = row.Cells[colReferenceTable.Name].Tag as Table;

                key.OldName = tag?.OldName;
                key.Name = keyName;
                key.Columns = tag?.Columns;
                key.ReferencedSchema = referenceTable.Schema;
                key.ReferencedTableName = referenceTable.Name;
                key.UpdateCascade = DataGridViewHelper.GetCellBoolValue(row, colUpdateCascade.Name);
                key.DeleteCascade = DataGridViewHelper.GetCellBoolValue(row, colDeleteCascade.Name);
                key.Comment = DataGridViewHelper.GetCellStringValue(row, colComment.Name);

                row.Tag = key;

                keyDesingerInfos.Add(key);
            }
        }

        return keyDesingerInfos;
    }

    private string GetColumnMappingsDisplayText(IEnumerable<ForeignKeyColumn> columns)
    {
        return string.Join(",", columns.Select(item => $"{item.ColumnName}=>{item.ReferencedColumnName}"));
    }

    private void dgvForeignKeys_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) DeleteRow();
    }

    private void DeleteRow()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvForeignKeys);

        if (row != null && !row.IsNewRow) dgvForeignKeys.Rows.RemoveAt(row.Index);
    }

    private void tsmiDeleteForeignKey_Click(object sender, EventArgs e)
    {
        DeleteRow();
    }

    private void dgvForeignKeys_SizeChanged(object sender, EventArgs e)
    {
        AutoSizeColumns();
    }

    private void AutoSizeColumns()
    {
        DataGridViewHelper.AutoSizeLastColumn(dgvForeignKeys);
    }

    private void dgvForeignKeys_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvForeignKeys_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colColumns.Index)
        {
            var row = dgvForeignKeys.Rows[e.RowIndex];

            var keyName = DataGridViewHelper.GetCellStringValue(row, colKeyName.Name);

            var table = row.Cells[colReferenceTable.Name].Tag as Table;

            var referenceTableName = DataGridViewHelper.GetCellStringValue(row, colReferenceTable.Name);

            if (!string.IsNullOrEmpty(keyName) && !string.IsNullOrEmpty(referenceTableName))
            {
                OnColumnMappingSelect?.Invoke(table.Schema, table.Name, (row.Tag as TableForeignKeyDesignerInfo)?.Columns);
            }
        }
    }

    public void SetRowColumns(IEnumerable<ForeignKeyColumn> mappings)
    {
        var cell = dgvForeignKeys.CurrentCell;

        if (cell != null)
        {
            cell.Value = GetColumnMappingsDisplayText(mappings);

            var keyDesignerInfo = cell.OwningRow.Tag as TableForeignKeyDesignerInfo;

            if (keyDesignerInfo == null) keyDesignerInfo = new TableForeignKeyDesignerInfo();

            keyDesignerInfo.Columns = mappings.ToList();

            cell.OwningRow.Tag = keyDesignerInfo;
        }
    }

    public void OnSaved()
    {
        for (var i = 0; i < dgvForeignKeys.RowCount; i++)
        {
            var row = dgvForeignKeys.Rows[i];

            var keyDesingerInfo = row.Tag as TableForeignKeyDesignerInfo;

            if (keyDesingerInfo != null && !string.IsNullOrEmpty(keyDesingerInfo.Name))
                keyDesingerInfo.OldName = keyDesingerInfo.Name;
        }
    }

    public void EndEdit()
    {
        dgvForeignKeys.EndEdit();
        dgvForeignKeys.CurrentCell = null;
    }

    private void dgvForeignKeys_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = dgvForeignKeys.Rows[e.RowIndex];

        if (e.ColumnIndex == colReferenceTable.Index)
        {
            var referenceTable = row.Cells[colReferenceTable.Name].Value as Table;

            var keyName = DataGridViewHelper.GetCellStringValue(row, colKeyName.Name);

            if (referenceTable != null)
            {
                if (string.IsNullOrEmpty(keyName))
                    row.Cells[colKeyName.Name].Value =
                        IndexManager.GetForeignKeyDefaultName(Table.Name, referenceTable.Name);

                row.Cells[colReferenceTable.Name].Tag = referenceTable;
            }
        }
    }

    private void dgvForeignKeys_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var row = DataGridViewHelper.GetSelectedRow(dgvForeignKeys);

            if (row != null)
            {
                var isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                tsmiDeleteForeignKey.Enabled = !isEmptyNewRow;
            }
            else
            {
                tsmiDeleteForeignKey.Enabled = false;
            }

            contextMenuStrip1.Show(dgvForeignKeys, e.Location);
        }
    }

    private void dgvForeignKeys_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        dgvForeignKeys.EndEdit();
        dgvForeignKeys.CurrentCell = null;
        dgvForeignKeys.Rows[e.RowIndex].Selected = true;
    }

    private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
    {
        OnGenerateChangeScripts?.Invoke();
    }
}

public class TableDisplayInfo
{
    public string DisplayName { get; set; }
    public Table Table { get; set; }
}