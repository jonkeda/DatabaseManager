using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseInterpreter.Model;

namespace DatabaseManager;

public partial class frmColumnMapping : Form
{
    private const string EmptyItem = "<None>";

    public frmColumnMapping()
    {
        InitializeComponent();
    }

    public string ReferenceTableName { get; set; }
    public string TableName { get; set; }
    public List<string> ReferenceTableColumns { get; set; } = new();
    public List<string> TableColumns { get; set; } = new();

    public List<ForeignKeyColumn> Mappings { get; set; } = new();

    private void frmColumnMapping_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        gbReferenceTable.Text = ReferenceTableName;
        gbTable.Text = TableName;

        LoadMappings();
    }

    private void LoadMappings()
    {
        if (Mappings != null)
            foreach (var mapping in Mappings)
            {
                var refCombo = CreateCombobox(panelReferenceTable, ReferenceTableColumns, mapping.ReferencedColumnName);

                panelReferenceTable.Controls.Add(refCombo);

                var combo = CreateCombobox(panelTable, TableColumns, mapping.ColumnName);

                panelTable.Controls.Add(combo);
            }

        var refComboEmpty = CreateCombobox(panelReferenceTable, ReferenceTableColumns, null);

        panelReferenceTable.Controls.Add(refComboEmpty);

        var comboEmpty = CreateCombobox(panelTable, TableColumns, null);

        panelTable.Controls.Add(comboEmpty);
    }

    private ComboBox CreateCombobox(Panel panel, List<string> values, string value)
    {
        var combo = new ComboBox();
        combo.MouseClick += Combo_MouseClick;
        combo.DropDownStyle = ComboBoxStyle.DropDown;
        combo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        combo.Width = panelTable.Width - 5;
        combo.Tag = panel.Controls.Count + 1;
        combo.SelectedIndexChanged += Combo_SelectedIndexChanged;
        combo.KeyPress += Combo_KeyPress;

        combo.Items.AddRange(GetValuesWithEmptyItem(values).Except(GetExistingValues(panel)).ToArray());

        if (panel.Controls.Count > 0) combo.Top = panel.Controls.Count * combo.Height + panel.Controls.Count;

        if (!string.IsNullOrEmpty(value) && values.Contains(value)) combo.Text = value;

        return combo;
    }

    private void Combo_MouseClick(object sender, MouseEventArgs e)
    {
        var combo = sender as ComboBox;

        if (!combo.DroppedDown) combo.DroppedDown = true;
    }

    private void Combo_KeyPress(object sender, KeyPressEventArgs e)
    {
        e.Handled = true;
    }

    private List<string> GetExistingValues(Panel panel)
    {
        var values = new List<string>();
        var comboboxes = panel.Controls.OfType<ComboBox>();

        foreach (var combo in comboboxes)
            if (!string.IsNullOrEmpty(combo.Text) && combo.Text != EmptyItem)
                values.Add(combo.Text);

        return values;
    }

    private List<string> GetValuesWithEmptyItem(List<string> values)
    {
        var cloneValues = values.Select(item => item).ToList();
        cloneValues.Add(EmptyItem);

        return cloneValues;
    }

    private void Combo_SelectedIndexChanged(object sender, EventArgs e)
    {
        var combo = sender as ComboBox;

        if (combo.Parent == null) return;

        var order = Convert.ToInt32(combo.Tag);

        if (order == combo.Parent.Controls.Count)
        {
            var refCombo = CreateCombobox(panelReferenceTable, ReferenceTableColumns, null);

            panelReferenceTable.Controls.Add(refCombo);

            var cbo = CreateCombobox(panelTable, TableColumns, null);

            panelTable.Controls.Add(cbo);
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        var mappings = new List<ForeignKeyColumn>();

        for (var i = 0; i < panelReferenceTable.Controls.Count; i++)
        {
            var refCombo = panelReferenceTable.Controls[i] as ComboBox;
            var combo = panelTable.Controls[i] as ComboBox;

            if (!string.IsNullOrEmpty(refCombo.Text) && refCombo.Text != EmptyItem
                                                     && !string.IsNullOrEmpty(combo.Text) && combo.Text != EmptyItem)
            {
                var mapping = new ForeignKeyColumn();
                mapping.ReferencedColumnName = refCombo.Text;
                mapping.ColumnName = combo.Text;
                mapping.Order = mappings.Count + 1;

                mappings.Add(mapping);
            }
        }

        Mappings = mappings;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }
}