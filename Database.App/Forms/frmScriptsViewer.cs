using System;
using System.Windows.Forms;
using DatabaseManager.Helper;
using Databases.Model.Enum;

namespace DatabaseManager;

public partial class frmScriptsViewer : Form
{
    public frmScriptsViewer()
    {
        InitializeComponent();
    }

    public DatabaseType DatabaseType { get; set; }

    private void frmScriptsViewer_Load(object sender, EventArgs e)
    {
    }

    public void LoadScripts(string scripts)
    {
        txtScripts.AppendText(scripts);

        RichTextBoxHelper.Highlighting(txtScripts, DatabaseType, false);
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}