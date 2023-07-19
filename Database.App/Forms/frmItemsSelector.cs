using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DatabaseManager.Helper;

namespace DatabaseManager;

public partial class frmItemsSelector : Form
{
    private bool isChecking;
    private readonly List<CheckItemInfo> items;

    public frmItemsSelector(List<CheckItemInfo> items)
    {
        InitializeComponent();
        this.items = items;
    }

    public frmItemsSelector(string title, List<CheckItemInfo> items)
    {
        InitializeComponent();

        Text = title;
        this.items = items;
    }

    public bool Required { get; set; } = true;

    public List<CheckItemInfo> CheckedItem { get; set; } = new();

    private void frmItemsSelector_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        foreach (var item in items) chkItems.Items.Add(item.Name, item.Checked);

        if (items.All(item => item.Checked)) chkSelectAll.Checked = true;
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        if (Required && chkItems.CheckedItems.Count == 0)
        {
            MessageBox.Show("Please select a item.");
            return;
        }

        foreach (var item in chkItems.CheckedItems)
            CheckedItem.Add(new CheckItemInfo { Name = item.ToString(), Checked = true });

        DialogResult = DialogResult.OK;

        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void chkSelectAll_CheckedChanged(object sender, EventArgs e)
    {
        CheckItems(chkSelectAll.Checked);
    }

    private void CheckItems(bool @checked)
    {
        if (!isChecking)
            for (var i = 0; i < chkItems.Items.Count; i++)
                chkItems.SetItemChecked(i, @checked);
    }

    private void chkItems_MouseUp(object sender, MouseEventArgs e)
    {
        HandleItemChecked();
    }

    private void chkItems_KeyUp(object sender, KeyEventArgs e)
    {
        HandleItemChecked();
    }

    private void HandleItemChecked()
    {
        isChecking = true;
        chkSelectAll.Checked = chkItems.CheckedItems.Count == chkItems.Items.Count;
        isChecking = false;
    }
}