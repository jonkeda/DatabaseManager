using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Microsoft.SqlServer.Types;
using PgGeom = NetTopologySuite.Geometries;

namespace DatabaseManager.Helper;

public class DataGridViewHelper
{
    public static DataGridViewRow GetSelectedRow(DataGridView dgv)
    {
        if (dgv.SelectedRows.Count > 0) return dgv.SelectedRows.OfType<DataGridViewRow>().First();

        return null;
    }

    public static DataGridViewRow GetCurrentRow(DataGridView dgv)
    {
        return dgv.CurrentRow;
    }

    public static DataTable ConvertDataTable(DataTable dataTable)
    {
        var changedColumns = new Dictionary<int, DataTableColumnChangeInfo>();
        var changedValues = new Dictionary<(int RowIndex, int ColumnIndex), dynamic>();

        var rowIndex = 0;

        foreach (DataRow row in dataTable.Rows)
        {
            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                var value = row[i];

                if (value != null)
                {
                    var type = value.GetType();

                    if (type != typeof(DBNull))
                    {
                        Type newColumnType = null;
                        object newValue = null;

                        if (type == typeof(byte[]))
                        {
                            newColumnType = typeof(string);
                            newValue = ValueHelper.BytesToHexString(value as byte[]);
                        }
                        else if (type == typeof(BitArray))
                        {
                            newColumnType = typeof(string);

                            var bitArray = value as BitArray;
                            var bytes = new byte[bitArray.Length];
                            bitArray.CopyTo(bytes, 0);

                            newValue = ValueHelper.BytesToHexString(bytes);
                        }
                        else if (type == typeof(SqlGeography))
                        {
                            newColumnType = typeof(string);
                            var geography = value as SqlGeography;
                            newValue = geography.IsNull ? "" : geography.ToString();
                        }
                        else if (type == typeof(SqlGeometry))
                        {
                            newColumnType = typeof(string);
                            var geom = value as SqlGeometry;
                            newValue = geom.IsNull ? "" : geom.ToString();
                        }
                        else if (type == typeof(PgGeom.Geometry))
                        {
                            newColumnType = typeof(string);
                            newValue = (value as PgGeom.Geometry)?.AsText();
                        }

                        if (newColumnType != null && !changedColumns.ContainsKey(i))
                            changedColumns.Add(i, new DataTableColumnChangeInfo { Type = newColumnType });

                        if (newValue != null) changedValues.Add((rowIndex, i), newValue);
                    }
                }
            }

            rowIndex++;
        }

        if (changedColumns.Count == 0)
            return dataTable;
        foreach (var i in changedColumns.Keys)
        {
            var column = dataTable.Columns[i];

            var dataTypeInfo = column.ExtendedProperties[nameof(DataTypeInfo)];

            if (dataTypeInfo == null)
                column.ExtendedProperties[nameof(DataTypeInfo)] = new DataTypeInfo { DataType = column.DataType.Name };
        }

        var dtChanged = DataTableHelper.GetChangedDataTable(dataTable, changedColumns, changedValues);

        return dtChanged;
    }

    public static void FormatCell(DataGridView gridView, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value != null)
        {
            var type = e.Value.GetType();

            if (type != typeof(DBNull))
                if (type == typeof(string))
                {
                    var content = e.Value.ToString();

                    if (content.Length > 1000)
                    {
                        var cell = gridView.Rows[e.RowIndex].Cells[e.ColumnIndex];

                        cell.Tag = content;

                        e.Value = content.Substring(0, 1000) + "...";

                        e.CellStyle.ForeColor = Color.Red;

                        cell.ToolTipText = $"The text has been truncated, it's original length is {content.Length}.";
                    }
                }
        }
    }

    public static string GetCurrentCellValue(DataGridView gridView)
    {
        var cell = gridView.CurrentCell;

        if (cell != null)
        {
            var value = cell.Tag == null ? cell.Value?.ToString() : cell.Tag?.ToString();

            return value;
        }

        return string.Empty;
    }

    public static bool IsGeometryValue(DataGridView gridView)
    {
        var value = GetCurrentCellValue(gridView);

        if (string.IsNullOrEmpty(value)) return false;

        var typeNames = Enum.GetNames(typeof(OpenGisGeometryType));

        return typeNames.Any(item => value.StartsWith($"{item.ToUpper()}(") || value.StartsWith($"{item.ToUpper()} ("));
    }

    public static void ShowGeometryViewer(DataGridView gridView)
    {
        var cell = gridView.CurrentCell;

        if (cell != null)
        {
            var value = GetCurrentCellValue(gridView);

            if (string.IsNullOrEmpty(value)) return;

            var column = gridView.Columns[cell.ColumnIndex];

            var table = gridView.DataSource as DataTable;

            var dc = table.Columns.Cast<DataColumn>().FirstOrDefault(item => item.ColumnName == column.Name);

            var property = dc?.ExtendedProperties[nameof(DataTypeInfo)];

            if (property is DataTypeInfo dti)
            {
                var dataType = dti.DataType;

                var isGeography = dataType.ToLower().Contains("geography");

                var viewer = new frmWktViewer(isGeography, value);
                viewer.Show();
            }
        }
    }

    public static void ShowCellContent(DataGridView gridView)
    {
        var value = GetCurrentCellValue(gridView);

        if (!string.IsNullOrEmpty(value))
        {
            var frm = new frmTextContent(value);

            frm.Show();
        }
    }

    public static void AutoSizeLastColumn(DataGridView dgv)
    {
        if (dgv.Columns.OfType<DataGridViewColumn>().Where(item => item.Visible).Count() <= 1) return;

        var column = dgv.Columns.OfType<DataGridViewColumn>().LastOrDefault(item => item.Visible);

        var gridWidth = dgv.Width;
        var totalWidth = 0;
        var rowHeadersWidth = dgv.RowHeadersVisible ? dgv.RowHeadersWidth : 0;
        var width = 0;

        totalWidth += rowHeadersWidth;

        foreach (DataGridViewColumn col in dgv.Columns)
            if (col.Visible)
            {
                totalWidth += col.Width;

                if (col.Name != column.Name) width += col.Width;
            }

        if (totalWidth < gridWidth) column.Width = gridWidth - width - dgv.RowHeadersWidth;

        var vScrollBar = dgv.Controls.OfType<VScrollBar>().FirstOrDefault();
        var scrollBarWidth = 0;

        if (vScrollBar != null && vScrollBar.Visible) scrollBarWidth = vScrollBar.Width;

        if (scrollBarWidth > 0) column.Width -= scrollBarWidth;
    }

    public static void SetRowColumnsReadOnly(DataGridView dgv, DataGridViewRow row, bool readony,
        params DataGridViewColumn[] excludeColumns)
    {
        foreach (DataGridViewColumn column in dgv.Columns)
        {
            if (excludeColumns != null && excludeColumns.Contains(column)) continue;

            row.Cells[column.Name].ReadOnly = readony;
        }
    }

    public static string GetCellStringValue(DataGridViewRow row, string columnName)
    {
        if (row == null) return null;

        return GetCellStringValue(row.Cells[columnName]);
    }

    public static string GetCellStringValue(DataGridViewCell cell)
    {
        return cell.Value?.ToString()?.Trim();
    }

    public static bool GetCellBoolValue(DataGridViewRow row, string columnName)
    {
        return GetCellBoolValue(row.Cells[columnName]);
    }

    public static bool GetCellBoolValue(DataGridViewCell cell)
    {
        return IsTrueValue(cell.Value);
    }

    public static bool IsTrueValue(object value)
    {
        return value?.ToString() == "True";
    }

    public static bool IsEmptyRow(DataGridViewRow row)
    {
        if (row == null) return true;

        var visibleCount = 0;
        var emptyCount = 0;

        foreach (DataGridViewCell cell in row.Cells)
            if (cell.Visible)
            {
                visibleCount++;

                if (string.IsNullOrEmpty(cell.Value?.ToString())) emptyCount++;
            }

        return visibleCount == emptyCount;
    }

    public static void SetRowCellsReadOnly(DataGridViewRow row, bool readOnly)
    {
        if (row == null) return;

        foreach (DataGridViewCell cell in row.Cells) cell.ReadOnly = readOnly;
    }
}