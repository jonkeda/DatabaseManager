using System;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager;

public partial class frmDataFilterCondition : Form
{
    public frmDataFilterCondition()
    {
        InitializeComponent();
    }

    public DataGridViewColumn Column { get; set; }

    public QueryConditionItem Condition { get; set; }

    private void frmDataFilterCondition_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        cboOperator.SelectedIndex = -1;
        txtValue.Text = "";
        txtFrom.Text = "";
        txtTo.Text = "";
        txtValues.Text = "";
    }

    private void InitControls()
    {
        rbSingle.Checked = true;

        SetValue();
    }

    public void SetValue()
    {
        var condition = Condition;

        if (condition == null) return;

        if (condition.Mode == QueryConditionMode.Single)
        {
            rbSingle.Checked = true;
            cboOperator.Text = condition.Operator;
            txtValue.Text = condition.Value;
        }
        else if (condition.Mode == QueryConditionMode.Range)
        {
            rbRange.Checked = true;
            txtFrom.Text = condition.From;
            txtTo.Text = condition.To;
        }
        else if (condition.Mode == QueryConditionMode.Series)
        {
            rbSeries.Checked = true;
            txtValues.Text = string.Join(",", condition.Values);
        }

        SetControlEnabled();
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        QueryConditionItem condition;

        if (!BuildCondition(out condition)) return;

        Condition = condition;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool BuildCondition(out QueryConditionItem condition)
    {
        condition = new QueryConditionItem { ColumnName = Column.Name, DataType = Column.ValueType };

        if (rbSingle.Checked)
        {
            if (cboOperator.SelectedIndex >= 0)
            {
                if (string.IsNullOrEmpty(cboOperator.Text))
                {
                    MessageBox.Show("Value can't be empty.");
                    return false;
                }

                condition.Mode = QueryConditionMode.Single;
                condition.Operator = cboOperator.Text;
                condition.Value = txtValue.Text;
            }
            else
            {
                MessageBox.Show("Please select operator.");
                return false;
            }
        }
        else if (rbRange.Checked)
        {
            var from = txtFrom.Text;
            var to = txtTo.Text;

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                MessageBox.Show("From and To value can't be empty.");
                return false;
            }

            condition.Mode = QueryConditionMode.Range;
            condition.From = from.Trim();
            condition.To = to.Trim();
        }
        else if (rbSeries.Checked)
        {
            var values = txtValues.Text;

            if (string.IsNullOrWhiteSpace(values))
            {
                MessageBox.Show("Value can't be empty.");
                return false;
            }

            var items = values.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (items.Length == 0)
            {
                MessageBox.Show("Has no any valid value.");
                return false;
            }

            condition.Mode = QueryConditionMode.Series;
            condition.Values = items.ToList();
        }

        return true;
    }

    private string GetValue(string value)
    {
        var needQuoted = FrontQueryHelper.NeedQuotedForSql(Column.ValueType);

        return needQuoted ? $"'{value}'" : value;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void rbSingle_CheckedChanged(object sender, EventArgs e)
    {
        SetControlEnabled();
    }

    private void rbRange_CheckedChanged(object sender, EventArgs e)
    {
        SetControlEnabled();
    }

    private void rbSeries_CheckedChanged(object sender, EventArgs e)
    {
        SetControlEnabled();
    }

    private void SetControlEnabled()
    {
        panelSingle.Enabled = rbSingle.Checked;
        panelRange.Enabled = rbRange.Checked;
        panelSeries.Enabled = rbSeries.Checked;
    }
}