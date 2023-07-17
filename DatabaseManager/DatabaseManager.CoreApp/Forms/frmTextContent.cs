using System;
using System.Drawing;
using System.Windows.Forms;

namespace DatabaseManager;

public partial class frmTextContent : Form
{
    public frmTextContent()
    {
        InitializeComponent();
    }

    public frmTextContent(string content)
    {
        InitializeComponent();

        txtContent.Text = content;
    }

    public frmTextContent(string title, string content, bool isError = false)
    {
        InitializeComponent();

        Text = title;
        txtContent.Text = content;

        if (isError) txtContent.ForeColor = Color.Red;
    }

    private void frmTextContent_Load(object sender, EventArgs e)
    {
        txtContent.Select(0, 0);
    }

    private void btnCopy_Click(object sender, EventArgs e)
    {
        var content = txtContent.Text.Trim();

        if (string.IsNullOrEmpty(content))
        {
            MessageBox.Show("The content is empty.");
            return;
        }

        Clipboard.SetDataObject(content);
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}