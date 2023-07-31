using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Model;
using Databases.Manager.Condition;
using Databases.Manager.Model.Query;

namespace DatabaseManager;

public partial class frmDataFilter : Form
{
    public List<DataGridViewColumn> Columns = new();

    public frmDataFilter()
    {
        InitializeComponent();
    }

    public QueryConditionBuilder ConditionBuilder { get; set; }

    private void frmDataFilter_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        LoadColumnsTree();

        if (ConditionBuilder != null)
            foreach (var condition in ConditionBuilder.Conditions)
            {
                var column = Columns.FirstOrDefault(item => item.Name == condition.ColumnName);
                if (column != null) AddField(column, condition);
            }
    }

    private void LoadColumnsTree()
    {
        foreach (var column in Columns)
        {
            var node = new TreeNode(column.Name);
            node.ImageKey = "Column.png";
            node.SelectedImageKey = node.ImageKey;
            node.Tag = column;

            tvColumns.Nodes.Add(node);
        }
    }

    private void tvColumns_ItemDrag(object sender, ItemDragEventArgs e)
    {
        var node = e.Item as TreeNode;
        if (node?.Tag is DataGridViewColumn)
        {
            tvColumns.SelectedNode = node;
            DoDragDrop(e.Item, DragDropEffects.Copy);
        }
    }

    private void tvColumns_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node == tvColumns.SelectedNode && e.Node.Tag is DataGridViewColumn tag)
            AddField(tag);
    }

    private void AddField(DataGridViewColumn column, QueryConditionItem condition = null)
    {
        foreach (DataGridViewRow row in dgvFilter.Rows)
            if (row.Cells["ColumnName"].Value.ToString() == column.Name)
                return;

        var rowIndex = dgvFilter.Rows.Add(column.Name);

        dgvFilter.Rows[rowIndex].Tag = condition;

        if (condition != null) dgvFilter.Rows[rowIndex].Cells["Filter"].Value = condition.ToString();
    }

    private void dgvFilter_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        var column = dgvFilter.Columns[e.ColumnIndex];

        if (column.Name == "Filter" && e.RowIndex > -1 && e.ColumnIndex > -1)
        {
            var columnName = dgvFilter.Rows[e.RowIndex].Cells["ColumnName"].Value.ToString();

            var filterCondition = new frmDataFilterCondition
                { Column = Columns.FirstOrDefault(item => item.Name == columnName) };
            filterCondition.Condition = dgvFilter.Rows[e.RowIndex].Tag as QueryConditionItem;

            if (filterCondition.ShowDialog() == DialogResult.OK)
            {
                var condition = filterCondition.Condition;

                dgvFilter.Rows[e.RowIndex].Tag = condition;

                dgvFilter.Rows[e.RowIndex].Cells["Filter"].Value = condition.ToString();
            }
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        GetQueryConditionBuilder();

        if (ConditionBuilder.Conditions.Count == 0)
            if (MessageBox.Show("Has no any condition, are you sure to query without any condition?", "Confirm",
                    MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

        DialogResult = DialogResult.OK;

        Close();
    }

    private QueryConditionBuilder GetQueryConditionBuilder()
    {
        ConditionBuilder = new QueryConditionBuilder();

        foreach (DataGridViewRow row in dgvFilter.Rows)
        {
            var condition = row.Tag as QueryConditionItem;

            if (condition != null) ConditionBuilder.Add(condition);
        }

        return ConditionBuilder;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void dgvFilter_DragEnter(object sender, DragEventArgs e)
    {
        var node = e.Data.GetData(typeof(TreeNode)) as TreeNode;
        if (node?.Tag is DataGridViewColumn)
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }

        var row = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
        if (row != null)
            if (dgvFilter.Rows.Contains(row))
            {
                e.Effect = DragDropEffects.Move;
            }
    }

    private void dgvFilter_DragOver(object sender, DragEventArgs e)
    {
        var row = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;

        if (row != null)
        {
            var pt = dgvFilter.PointToClient(new Point(e.X, e.Y));
            var ht = dgvFilter.HitTest(pt.X, pt.Y);

            switch (ht.Type)
            {
                case DataGridViewHitTestType.Cell:
                case DataGridViewHitTestType.RowHeader:
                case DataGridViewHitTestType.None:
                    e.Effect = DragDropEffects.Move;
                    break;
                default:
                    e.Effect = DragDropEffects.None;
                    break;
            }
        }
    }

    private void dgvFilter_DragDrop(object sender, DragEventArgs e)
    {
        var node = e.Data.GetData(typeof(TreeNode)) as TreeNode;

        if (node != null) AddField(node.Tag as DataGridViewColumn);
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        dgvFilter.Rows.Clear();
    }

    private void btnRemove_Click(object sender, EventArgs e)
    {
        var count = dgvFilter.SelectedRows.Count;
        if (count == 0)
        {
            MessageBox.Show("Please select a row first.");
            return;
        }

        dgvFilter.Rows.RemoveAt(dgvFilter.SelectedRows[0].Index);
    }
}