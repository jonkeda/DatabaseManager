using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using View = DatabaseInterpreter.Model.View;

namespace DatabaseManager.Controls;

public delegate void DataFilterHandler(object sender);

public partial class UC_DataViewer : UserControl, IDbObjContentDisplayer
{
    private DatabaseObjectDisplayInfo displayInfo;
    private bool isSorting;
    public DataFilterHandler OnDataFilter;
    private int sortedColumnIndex = -1;
    private SortOrder sortOrder = SortOrder.None;

    public UC_DataViewer()
    {
        InitializeComponent();

        pagination.PageSize = 50;

        dgvData.AutoGenerateColumns = true;

        typeof(DataGridView).InvokeMember("DoubleBuffered",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dgvData,
            new object[] { true });
    }

    public IEnumerable<DataGridViewColumn> Columns => dgvData.Columns.Cast<DataGridViewColumn>();
    public QueryConditionBuilder ConditionBuilder { get; private set; }

    public void Show(DatabaseObjectDisplayInfo displayInfo)
    {
        LoadData(displayInfo);
    }

    public ContentSaveResult Save(ContentSaveInfo info)
    {
        DataTableHelper.WriteToFile(dgvData.DataSource as DataTable, info.FilePath);

        return new ContentSaveResult { IsOK = true };
    }

    private async void LoadData(DatabaseObjectDisplayInfo displayInfo, long pageNum = 1, bool isSort = false)
    {
        this.displayInfo = displayInfo;

        pagination.PageNum = pageNum;

        var dbObject = displayInfo.DatabaseObject;

        var pageSize = pagination.PageSize;

        var option = new DbInterpreterOption { ShowTextForGeometry = true };

        var dbInterpreter =
            DbInterpreterHelper.GetDbInterpreter(displayInfo.DatabaseType, displayInfo.ConnectionInfo, option);

        var orderColumns = "";

        if (dgvData.SortedColumn != null)
        {
            var sortOrder = this.sortOrder == SortOrder.Descending ? "DESC" : "ASC";
            orderColumns = $"{dbInterpreter.GetQuotedString(dgvData.SortedColumn.Name)} {sortOrder}";
        }

        var conditionClause = "";

        if (ConditionBuilder != null && ConditionBuilder.Conditions.Count > 0)
        {
            ConditionBuilder.DatabaseType = dbInterpreter.DatabaseType;
            ConditionBuilder.QuotationLeftChar = dbInterpreter.QuotationLeftChar;
            ConditionBuilder.QuotationRightChar = dbInterpreter.QuotationRightChar;

            conditionClause = "WHERE " + ConditionBuilder;
        }

        try
        {
            var isForView = false;

            if (dbObject is View)
            {
                dbObject = ObjectHelper.CloneObject<Table>(dbObject);

                isForView = true;
            }

            (long Total, DataTable Data) result = await dbInterpreter.GetPagedDataTableAsync(dbObject as Table,
                orderColumns, pageSize, pageNum, conditionClause, isForView);

            pagination.TotalCount = result.Total;

            dgvData.DataSource = DataGridViewHelper.ConvertDataTable(result.Data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex), "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        foreach (DataGridViewColumn column in dgvData.Columns)
        {
            var valueType = column.ValueType;

            if (valueType == typeof(byte[]) || DataTypeHelper.IsGeometryType(valueType.Name))
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        if (sortedColumnIndex != -1)
        {
            var column = dgvData.Columns[sortedColumnIndex];

            isSorting = true;

            var sortDirection = GetSortDirection(sortOrder);

            dgvData.Sort(column, sortDirection);

            isSorting = false;
        }
    }

    private ListSortDirection GetSortDirection(SortOrder sortOrder)
    {
        return sortOrder == SortOrder.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
    }

    private void pagination_OnPageNumberChanged(long pageNum)
    {
        LoadData(displayInfo, pageNum);
    }

    private void dgvData_Sorted(object sender, EventArgs e)
    {
        if (isSorting) return;

        sortedColumnIndex = dgvData.SortedColumn.DisplayIndex;
        sortOrder = dgvData.SortOrder;

        LoadData(displayInfo, 1, true);
    }

    private void btnFilter_Click(object sender, EventArgs e)
    {
        if (OnDataFilter != null) OnDataFilter(this);
    }

    public void FilterData(QueryConditionBuilder conditionBuilder)
    {
        ConditionBuilder = conditionBuilder;

        LoadData(displayInfo);
    }

    private void dgvData_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
    }

    private void dgvData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        DataGridViewHelper.FormatCell(dgvData, e);
    }

    private void dgvData_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var value = dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

        if (value != null)
            if (value.GetType() != typeof(DBNull))
                if (e.Button == MouseButtons.Right)
                {
                    dgvData.CurrentCell = dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex];

                    SetContextMenuItemVisible();

                    cellContextMenu.Show(Cursor.Position);
                }
    }

    private void SetContextMenuItemVisible()
    {
        tsmiViewGeometry.Visible = DataGridViewHelper.IsGeometryValue(dgvData);
    }

    private void tsmiCopy_Click(object sender, EventArgs e)
    {
        var value = DataGridViewHelper.GetCurrentCellValue(dgvData);

        if (!string.IsNullOrEmpty(value)) Clipboard.SetDataObject(value);
    }

    private void tsmiViewGeometry_Click(object sender, EventArgs e)
    {
        DataGridViewHelper.ShowGeometryViewer(dgvData);
    }

    private void tsmiShowContent_Click(object sender, EventArgs e)
    {
        DataGridViewHelper.ShowCellContent(dgvData);
    }
}