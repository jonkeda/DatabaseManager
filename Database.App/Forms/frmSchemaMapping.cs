using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Databases.Model.Schema;

namespace DatabaseManager;

public partial class frmSchemaMapping : Form
{
    private const string EmptyItem = "<None>";

    public frmSchemaMapping()
    {
        InitializeComponent();
    }

    internal List<string> SourceSchemas { get; set; } = new();
    internal List<string> TargetSchemas { get; set; } = new();
    internal List<SchemaMappingInfo> Mappings { get; set; } = new();

    private void frmColumnMapping_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        LoadMappings();
    }

    private void LoadMappings()
    {
        if (Mappings != null)
            foreach (var mapping in Mappings)
            {
                var sourceCombo = CreateCombobox(panelSourceSchema, SourceSchemas, mapping.SourceSchema);

                panelSourceSchema.Controls.Add(sourceCombo);

                var targetCombo = CreateCombobox(panelTargetSchema, TargetSchemas, mapping.TargetSchema);

                panelTargetSchema.Controls.Add(targetCombo);
            }

        CreatePlaceholder();
    }

    private void CreatePlaceholder()
    {
        var sourceComboEmpty = CreateCombobox(panelSourceSchema, SourceSchemas, null);

        panelSourceSchema.Controls.Add(sourceComboEmpty);

        var targetComboEmpty = CreateCombobox(panelTargetSchema, TargetSchemas, null);

        panelTargetSchema.Controls.Add(targetComboEmpty);
    }

    private ComboBox CreateCombobox(Panel panel, List<string> values, string value)
    {
        var combo = new ComboBox();
        combo.MouseClick += Combo_MouseClick;
        combo.DropDownStyle = ComboBoxStyle.DropDown;
        combo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        combo.Width = panel.Width - 5;
        combo.Tag = panel.Controls.Count + 1;
        combo.SelectedIndexChanged += Combo_SelectedIndexChanged;

        var displayValues = GetValuesWithEmptyItem(values).AsEnumerable();

        if (panel.Name == panelSourceSchema.Name) displayValues = displayValues.Except(GetExistingValues(panel));

        combo.Items.AddRange(displayValues.ToArray());

        if (panel.Controls.Count > 0) combo.Top = panel.Controls.Count * combo.Height + panel.Controls.Count;

        if (!string.IsNullOrEmpty(value) && values.Contains(value)) combo.Text = value;

        return combo;
    }

    private void Combo_MouseClick(object sender, MouseEventArgs e)
    {
        var combo = sender as ComboBox;

        if (!combo.DroppedDown) combo.DroppedDown = true;
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
            var sourceCombo = CreateCombobox(panelSourceSchema, SourceSchemas, null);

            panelSourceSchema.Controls.Add(sourceCombo);

            var targetCombo = CreateCombobox(panelTargetSchema, TargetSchemas, null);

            panelTargetSchema.Controls.Add(targetCombo);
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        var mappings = new List<SchemaMappingInfo>();

        for (var i = 0; i < panelSourceSchema.Controls.Count; i++)
        {
            var sourceCombo = panelSourceSchema.Controls[i] as ComboBox;
            var targetCombo = panelTargetSchema.Controls[i] as ComboBox;

            if (sourceCombo.Text != EmptyItem && targetCombo.Text != EmptyItem &&
                !(string.IsNullOrEmpty(sourceCombo.Text) && string.IsNullOrEmpty(targetCombo.Text)))
            {
                var mapping = new SchemaMappingInfo();
                mapping.SourceSchema = sourceCombo.Text;
                mapping.TargetSchema = targetCombo.Text;

                mappings.Add(mapping);
            }
        }

        if (mappings.Any(item =>
                !string.IsNullOrEmpty(item.SourceSchema) && item.SourceSchema != EmptyItem &&
                mappings.Count(t => t.SourceSchema == item.SourceSchema) > 1))
        {
            MessageBox.Show("One Source Schema can't be mapped to more than one target schema!");
            return;
        }

        Mappings = mappings;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void btnAutoMap_Click(object sender, EventArgs e)
    {
        ClearControls();

        foreach (var sourceSchema in SourceSchemas)
            if (TargetSchemas.Any(item => item == sourceSchema))
            {
                var sourceCombo = CreateCombobox(panelSourceSchema, SourceSchemas, sourceSchema);

                panelSourceSchema.Controls.Add(sourceCombo);

                var targetCombo = CreateCombobox(panelTargetSchema, TargetSchemas, sourceSchema);

                panelTargetSchema.Controls.Add(targetCombo);
            }

        CreatePlaceholder();
    }

    private void ClearControls()
    {
        panelSourceSchema.Controls.Clear();
        panelTargetSchema.Controls.Clear();
    }

    private void btnReset_Click(object sender, EventArgs e)
    {
        ClearControls();
        CreatePlaceholder();
    }
}