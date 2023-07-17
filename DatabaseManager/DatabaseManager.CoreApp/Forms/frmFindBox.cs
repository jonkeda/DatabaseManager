using System;
using System.Windows.Forms;

namespace DatabaseManager.Forms;

public delegate void FindBoxHandler();

public delegate void FindBoxClosedHandler();

public partial class frmFindBox : Form
{
    private readonly bool showOptions;

    public frmFindBox(bool showOptions = false)
    {
        InitializeComponent();

        this.showOptions = showOptions;
    }

    public string FindWord { get; private set; }
    public bool MatchCase { get; private set; }
    public bool MatchWholeWord { get; private set; }
    public event FindBoxHandler OnFind;
    public event FindBoxClosedHandler OnEndFind;

    private void frmFindBox_Load(object sender, EventArgs e)
    {
        InitControls();
    }

    private void InitControls()
    {
        if (!showOptions)
        {
            optionsPanel.Visible = false;
            Height -= optionsPanel.Height;
        }
        else
        {
            chkMatchWholeWord.Checked = true;
        }

        txtWord.Focus();
    }

    private void txtWord_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) Find();
    }

    private void btnFind_Click(object sender, EventArgs e)
    {
        Find();
    }

    private void Find()
    {
        var word = txtWord.Text.Trim();

        if (string.IsNullOrEmpty(word))
        {
            MessageBox.Show("Please enter a word.");
            return;
        }

        FindWord = word;
        MatchCase = chkMatchCase.Checked;
        MatchWholeWord = chkMatchWholeWord.Checked;

        if (OnFind != null)
        {
            OnFind();

            btnFind.Focus();
        }
        else
        {
            Close();

            DialogResult = DialogResult.OK;
        }
    }

    private void frmFindBox_FormClosed(object sender, FormClosedEventArgs e)
    {
        if (OnEndFind != null) OnEndFind();
    }
}