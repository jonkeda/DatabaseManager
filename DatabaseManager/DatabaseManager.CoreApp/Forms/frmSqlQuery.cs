using System;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager;

public partial class frmSqlQuery : Form
{
    public frmSqlQuery()
    {
        InitializeComponent();
    }

    public bool ReadOnly { get; set; }
    public int SplitterDistance { get; set; }
    public bool ShowEditorMessage { get; set; }

    private void frmSqlQuery_Load(object sender, EventArgs e)
    {
    }

    public void Init()
    {
        ucSqlQuery.ReadOnly = ReadOnly;
        ucSqlQuery.ShowEditorMessage = ShowEditorMessage;
        ucSqlQuery.SplitterDistance = SplitterDistance;
    }

    public void Query(DatabaseObjectDisplayInfo displayInfo)
    {
        ucSqlQuery.Editor.AppendText(displayInfo.Content);

        RichTextBoxHelper.Highlighting(ucSqlQuery.Editor, displayInfo.DatabaseType, false);

        ucSqlQuery.RunScripts(displayInfo);
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}