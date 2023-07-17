using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Controls;

public partial class UC_TableColumns : UserControl
{
    private IEnumerable<DataTypeSpecification> dataTypeSpecifications;
    private readonly bool defaultNullable = true;
    private Rectangle dragBoxFromMouseDown;

    public GeneateChangeScriptsHandler OnGenerateChangeScripts;
    private int rowIndexFromMouseDown;
    private int rowIndexOfItemUnderMouseToDrop;

    public UC_TableColumns()
    {
        InitializeComponent();

        colIdentity.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
    }

    public DatabaseType DatabaseType { get; set; }
    public List<UserDefinedType> UserDefinedTypes { get; set; }

    private void UC_TableColumns_Load(object sender, EventArgs e)
    {
        InitColumnsGrid();
    }

    public void InitControls()
    {
        if (!ManagerUtil.SupportComment(DatabaseType))
            if (colComment.Visible)
            {
                colComment.Visible = false;
                colColumnName.Width += 50;
                colDataType.Width += 50;
                colDefaultValue.Width += 50;
            }

        LoadDataTypes();
    }

    private void InitColumnsGrid()
    {
        foreach (DataGridViewRow row in dgvColumns.Rows)
        {
            if (row.Tag == null)
            {
                if (row.IsNewRow) row.Cells[colNullable.Name].Value = defaultNullable;

                row.Tag = new TableColumnDesingerInfo { IsNullable = defaultNullable };
            }

            var columnName = row.Cells[nameof(colColumnName)].Value?.ToString();

            if (string.IsNullOrEmpty(columnName))
                DataGridViewHelper.SetRowColumnsReadOnly(dgvColumns, row, true, colColumnName);
        }
    }

    public void LoadColumns(Table table, IEnumerable<TableColumnDesingerInfo> columns)
    {
        dgvColumns.Rows.Clear();

        foreach (var column in columns)
        {
            var rowIndex = dgvColumns.Rows.Add();

            var row = dgvColumns.Rows[rowIndex];

            row.Cells[colColumnName.Name].Value = column.Name;
            row.Cells[colDataType.Name].Value = column.DataType;
            row.Cells[colLength.Name].Value = column.Length;
            row.Cells[colNullable.Name].Value = column.IsNullable;
            row.Cells[colIdentity.Name].Value = column.IsIdentity;
            row.Cells[colPrimary.Name].Value = column.IsPrimary;
            row.Cells[colDefaultValue.Name].Value = StringHelper.GetBalanceParenthesisTrimedValue(column.DefaultValue);
            row.Cells[colComment.Name].Value = column.Comment;

            row.Tag = column;

            var extraPropertyInfo = new TableColumnExtraPropertyInfo();

            if (column.IsComputed) extraPropertyInfo.Expression = column.ComputeExp;

            if (column.IsIdentity && table.IdentitySeed.HasValue)
            {
                extraPropertyInfo.Seed = table.IdentitySeed.Value;
                extraPropertyInfo.Increment = table.IdentityIncrement.Value;
            }

            SetColumnCellsReadonly(row);
        }

        AutoSizeColumns();
        dgvColumns.ClearSelection();
    }

    public void OnSaved()
    {
        for (var i = 0; i < dgvColumns.RowCount; i++)
        {
            var row = dgvColumns.Rows[i];

            var columnDesingerInfo = row.Tag as TableColumnDesingerInfo;

            if (columnDesingerInfo != null && !string.IsNullOrEmpty(columnDesingerInfo.Name))
                columnDesingerInfo.OldName = columnDesingerInfo.Name;
        }
    }

    private void LoadDataTypes()
    {
        dataTypeSpecifications = DataTypeManager.GetDataTypeSpecifications(DatabaseType);

        var dbObjects = new List<DatabaseObject>();

        foreach (var dataTypeSpec in dataTypeSpecifications)
            dbObjects.Add(new DatabaseObject { Name = dataTypeSpec.Name });

        if (UserDefinedTypes != null)
            dbObjects.AddRange(UserDefinedTypes.GroupBy(item => item.Name)
                .Select(item => new DatabaseObject { Name = item.Key }));

        colDataType.DataSource = dbObjects;
        colDataType.DisplayMember = "Name";
        colDataType.ValueMember = "Name";
        colDataType.AutoComplete = true;
    }

    private UserDefinedType GetUserDefinedType(string dataType)
    {
        return UserDefinedTypes?.FirstOrDefault(item => item.Name == dataType);
    }

    public void EndEdit()
    {
        dgvColumns.EndEdit();
        dgvColumns.CurrentCell = null;
    }

    public List<TableColumnDesingerInfo> GetColumns()
    {
        var columnDesingerInfos = new List<TableColumnDesingerInfo>();

        var order = 1;
        foreach (DataGridViewRow row in dgvColumns.Rows)
        {
            var col = new TableColumnDesingerInfo { Order = order };

            var colName = row.Cells[colColumnName.Name].Value?.ToString();

            if (!string.IsNullOrEmpty(colName))
            {
                var tag = row.Tag as TableColumnDesingerInfo;

                var dataType = DataGridViewHelper.GetCellStringValue(row, colDataType.Name);

                col.OldName = tag?.OldName;
                col.Name = colName;
                col.DataType = dataType;
                col.Length = DataGridViewHelper.GetCellStringValue(row, colLength.Name);
                col.IsNullable = DataGridViewHelper.GetCellBoolValue(row, colNullable.Name);
                col.IsPrimary = DataGridViewHelper.GetCellBoolValue(row, colPrimary.Name);
                col.IsIdentity = DataGridViewHelper.GetCellBoolValue(row, colIdentity.Name);
                col.DefaultValue = DataGridViewHelper.GetCellStringValue(row, colDefaultValue.Name);
                col.Comment = DataGridViewHelper.GetCellStringValue(row, colComment.Name);
                col.ExtraPropertyInfo = tag?.ExtraPropertyInfo;

                var userDefinedType = GetUserDefinedType(dataType);

                if (userDefinedType != null)
                {
                    col.IsUserDefined = true;
                    col.DataTypeSchema = userDefinedType.Schema;
                }

                row.Tag = col;

                columnDesingerInfos.Add(col);

                order++;
            }
        }

        return columnDesingerInfos;
    }

    private void UC_TableColumns_SizeChanged(object sender, EventArgs e)
    {
        AutoSizeColumns();
    }

    private void AutoSizeColumns()
    {
        DataGridViewHelper.AutoSizeLastColumn(dgvColumns);
    }

    private void dgvColumns_UserAddedRow(object sender, DataGridViewRowEventArgs e)
    {
        e.Row.Cells[colNullable.Name].Value = defaultNullable;
        e.Row.Tag = new TableColumnDesingerInfo { IsNullable = defaultNullable };

        DataGridViewHelper.SetRowColumnsReadOnly(dgvColumns, e.Row, true, colColumnName);
    }

    private void dgvColumns_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            var row = dgvColumns.Rows[e.RowIndex];
            var cell = row.Cells[e.ColumnIndex];

            if (e.ColumnIndex == colColumnName.Index)
            {
                var columnName = cell.Value?.ToString();

                DataGridViewHelper.SetRowColumnsReadOnly(dgvColumns, row, string.IsNullOrEmpty(columnName),
                    colColumnName);
                SetColumnCellsReadonly(row);
            }
            else if (e.ColumnIndex == colDataType.Index || e.ColumnIndex == colDefaultValue.Index)
            {
                SetColumnCellsReadonly(row);
            }
            else if (e.ColumnIndex == colPrimary.Index)
            {
                var primaryCell = row.Cells[colPrimary.Name];
                var nullableCell = row.Cells[colNullable.Name];

                if (DataGridViewHelper.IsTrueValue(primaryCell.Value) &&
                    DataGridViewHelper.IsTrueValue(nullableCell.Value)) nullableCell.Value = false;
            }
            else if (e.ColumnIndex == colIdentity.Index)
            {
                if (DataGridViewHelper.IsTrueValue(cell.Value))
                    foreach (DataGridViewRow r in dgvColumns.Rows)
                        if (r.Index >= 0 && r.Index != e.RowIndex)
                            r.Cells[colIdentity.Name].Value = false;

                ShowColumnExtraPropertites();
            }
        }
    }

    private void SetColumnCellsReadonly(DataGridViewRow row)
    {
        var lengthCell = row.Cells[colLength.Name];
        var primaryCell = row.Cells[colPrimary.Name];
        var identityCell = row.Cells[colIdentity.Name];

        var dataType = DataGridViewHelper.GetCellStringValue(row, colDataType.Name);

        if (string.IsNullOrEmpty(dataType)) return;

        if (dataType.IndexOf('(') >= 0) dataType = dataType.Substring(0, dataType.IndexOf('('));

        if (!string.IsNullOrEmpty(dataType))
        {
            var userDefindedType = GetUserDefinedType(dataType);

            if (userDefindedType != null) dataType = userDefindedType.Attributes.First().DataType;

            var dataTypeSpec = dataTypeSpecifications.FirstOrDefault(item => item.Name == dataType);

            if (dataTypeSpec != null)
            {
                var isLengthReadOnly = userDefindedType != null || string.IsNullOrEmpty(dataTypeSpec.Args);
                var isPrimaryReadOnly = dataTypeSpec.IndexForbidden;
                var isIdentityReadOnly = !dataTypeSpec.AllowIdentity;

                lengthCell.ReadOnly = isLengthReadOnly;
                primaryCell.ReadOnly = isPrimaryReadOnly;

                if (DatabaseType == DatabaseType.Postgres)
                    if (ValueHelper.IsSequenceNextVal(row.Cells[colDefaultValue.Name].Value?.ToString()))
                        isIdentityReadOnly = true;

                identityCell.ReadOnly = isIdentityReadOnly;

                if (isLengthReadOnly) lengthCell.Value = null;

                if (isPrimaryReadOnly) primaryCell.Value = false;

                if (isIdentityReadOnly) identityCell.Value = false;
            }
        }
        else
        {
            lengthCell.ReadOnly = true;
            primaryCell.ReadOnly = true;
            identityCell.ReadOnly = true;
        }
    }

    private void dgvColumns_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvColumns_SelectionChanged(object sender, EventArgs e)
    {
    }

    private void ShowColumnExtraPropertites()
    {
        var row = DataGridViewHelper.GetCurrentRow(dgvColumns);

        if (row != null)
        {
            var column = row.Tag as TableColumnDesingerInfo;

            if (column == null)
            {
                column = new TableColumnDesingerInfo();
                row.Tag = column;
            }

            var extralProperty = column?.ExtraPropertyInfo;

            if (extralProperty == null)
            {
                extralProperty = new TableColumnExtraPropertyInfo();
                column.ExtraPropertyInfo = extralProperty;
            }

            var identityCell = row.Cells[colIdentity.Name];

            if (!DataGridViewHelper.IsTrueValue(identityCell.Value))
                columnPropertites.HiddenProperties = new[]
                    { nameof(extralProperty.Seed), nameof(extralProperty.Increment) };
            else
                columnPropertites.HiddenProperties = null;

            columnPropertites.SelectedObject = extralProperty;
            columnPropertites.Refresh();
        }
    }

    private void dgvColumns_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.ColumnIndex == colIdentity.Index) dgvColumns.EndEdit();
    }

    private void dgvColumns_DragDrop(object sender, DragEventArgs e)
    {
        var clientPoint = dgvColumns.PointToClient(new Point(e.X, e.Y));

        rowIndexOfItemUnderMouseToDrop = dgvColumns.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

        if (rowIndexOfItemUnderMouseToDrop == -1) return;

        if (rowIndexFromMouseDown >= 0 && rowIndexOfItemUnderMouseToDrop < dgvColumns.Rows.Count)
            if (dgvColumns.Rows[rowIndexOfItemUnderMouseToDrop].IsNewRow)
                return;

        if (e.Effect == DragDropEffects.Move)
        {
            var rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;

            if (rowToMove.Index >= 0)
            {
                dgvColumns.Rows.RemoveAt(rowIndexFromMouseDown);
                dgvColumns.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);

                var columnName = DataGridViewHelper.GetCellStringValue(rowToMove, colColumnName.Name);

                DataGridViewHelper.SetRowColumnsReadOnly(dgvColumns, rowToMove, string.IsNullOrEmpty(columnName),
                    colColumnName);
                SetColumnCellsReadonly(rowToMove);
            }
        }
    }

    private void dgvColumns_MouseMove(object sender, MouseEventArgs e)
    {
        if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            if (dragBoxFromMouseDown != Rectangle.Empty &&
                !dragBoxFromMouseDown.Contains(e.X, e.Y))
            {
                var dropEffect = dgvColumns.DoDragDrop(
                    dgvColumns.Rows[rowIndexFromMouseDown],
                    DragDropEffects.Move);
            }
    }

    private void dgvColumns_MouseDown(object sender, MouseEventArgs e)
    {
        var hit = dgvColumns.HitTest(e.X, e.Y);
        rowIndexFromMouseDown = hit.RowIndex;

        if (hit.Type == DataGridViewHitTestType.RowHeader && rowIndexFromMouseDown != -1)
        {
            var dragSize = SystemInformation.DragSize;

            dragBoxFromMouseDown = new Rectangle(
                new Point(
                    e.X - dragSize.Width / 2,
                    e.Y - dragSize.Height / 2),
                dragSize);
        }
        else
        {
            dragBoxFromMouseDown = Rectangle.Empty;
        }
    }

    private void dgvColumns_DragOver(object sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.Move;
    }

    private void tsmiInsertColumn_Click(object sender, EventArgs e)
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvColumns);

        if (row != null)
        {
            var rowIndex = row.Index < 0 ? 0 : row.Index;

            dgvColumns.Rows.Insert(rowIndex);
            dgvColumns.Rows[rowIndex].Selected = true;
            dgvColumns.Rows[rowIndex].Cells[colNullable.Name].Value = defaultNullable;
        }
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

            if (row != null)
            {
                var isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                tsmiDeleteColumn.Enabled = !isEmptyNewRow;
            }
            else
            {
                tsmiDeleteColumn.Enabled = false;
            }

            contextMenuStrip1.Show(dgvColumns, e.Location);
        }
    }

    private void dgvColumns_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) DeleteRow();
    }

    private void DeleteRow()
    {
        var row = DataGridViewHelper.GetSelectedRow(dgvColumns);

        if (row != null && !row.IsNewRow) dgvColumns.Rows.RemoveAt(row.Index);
    }

    private void dgvColumns_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        dgvColumns.EndEdit();
        dgvColumns.CurrentCell = null;
        dgvColumns.Rows[e.RowIndex].Selected = true;
    }

    private void dgvColumns_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        AutoSizeColumns();
        dgvColumns.ClearSelection();
    }

    private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
    {
        if (OnGenerateChangeScripts != null) OnGenerateChangeScripts();
    }

    private void dgvColumns_CellEnter(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        ShowColumnExtraPropertites();
    }

    private void dgvColumns_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (dgvColumns.CurrentCellAddress.X == colDataType.DisplayIndex)
        {
            var combo = e.Control as ComboBox;

            if (combo != null) combo.DropDownStyle = ComboBoxStyle.DropDown;
        }
    }

    private void dgvColumns_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
    {
        e.Row.Cells[colNullable.Name].Value = defaultNullable;
    }

    private void dgvColumns_CellLeave(object sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == colDataType.Index)
        {
            var cell = dgvColumns.CurrentCell;

            if (cell != null && cell.IsInEditMode && cell.EditedFormattedValue != cell.Value)
                cell.Value = cell.EditedFormattedValue;
        }
    }
}