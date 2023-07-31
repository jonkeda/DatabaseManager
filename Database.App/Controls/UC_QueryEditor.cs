using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Data;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using Databases.Config;
using Databases.Interpreter;
using Databases.Manager.Manager;
using Databases.Manager.Script;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;
using Databases.Model.Function;
using Databases.Model.Schema;

namespace DatabaseManager.Controls;

public delegate void QueryEditorInfoMessageHandler(string information);

public partial class UC_QueryEditor : UserControl
{
    private const int WordListMinWidth = 160;
    private List<SqlWord> allWords;
    private IEnumerable<FunctionSpecification> builtinFunctions;
    private List<string> dbSchemas;
    private bool enableIntellisense;
    private frmFindBox findBox;
    private bool intellisenseSetuped;
    private bool isPasting;
    private IEnumerable<string> keywords;
    private readonly string namePattern = @"\b([_a-zA-Z][_0-9a-zA-Z]+)\b";
    private Regex nameRegex = new(@"\b(^[_a-zA-Z][ _0-9a-zA-Z]+$)\b");
    private readonly string nameWithSpacePattern = @"\b([_a-zA-Z][ _0-9a-zA-Z]+)\b";

    public QueryEditorInfoMessageHandler OnQueryEditorInfoMessage;
    private SchemaInfo schemaInfo;

    public UC_QueryEditor()
    {
        InitializeComponent();

        lvWords.MouseWheel += LvWords_MouseWheel;
        panelWords.VerticalScroll.Enabled = true;
        panelWords.VerticalScroll.Visible = true;
    }

    private string commentString => RichTextBoxHelper.GetCommentString(DatabaseType);
    public DatabaseType DatabaseType { get; set; }
    public DbInterpreter DbInterpreter { get; set; }

    public RichTextBox Editor { get; private set; }

    public event EventHandler SetupIntellisenseRequired;

    public void Init()
    {
        keywords = KeywordManager.GetKeywords(DatabaseType);
        builtinFunctions = FunctionManager.GetFunctionSpecifications(DatabaseType);
    }

    private void LvWords_MouseWheel(object sender, MouseEventArgs e)
    {
        if (panelWords.Visible && txtToolTip.Visible) txtToolTip.Visible = false;
    }

    public void SetupIntellisence()
    {
        intellisenseSetuped = true;
        enableIntellisense = true;
        schemaInfo = DataStore.GetSchemaInfo(DatabaseType);
        allWords = SqlWordFinder.FindWords(DatabaseType, "");
        dbSchemas = allWords.Where(item => item.Type == SqlWordTokenType.Schema).Select(item => item.Text).ToList();
    }

    private void tsmiCopy_Click(object sender, EventArgs e)
    {
        CopyText();
    }

    private void CopyText()
    {
        Clipboard.SetDataObject(Editor.SelectedText);
    }

    private void tsmiPaste_Click(object sender, EventArgs e)
    {
        Editor.Paste();
    }

    private void txtEditor_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            tsmiCopy.Enabled = Editor.SelectionLength > 0;
            tsmiDisableIntellisense.Text = $"{(enableIntellisense ? "Disable" : "Enable")} Intellisense";
            tsmiUpdateIntellisense.Visible = enableIntellisense;
            editorContexMenu.Show(Editor, e.Location);
        }
    }

    private void txtEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            if (FormEventCenter.OnRunScripts != null) FormEventCenter.OnRunScripts();
        }
        else if (e.Control && e.KeyCode == Keys.V)
        {
            isPasting = true;
            return;
        }
        else if (e.Control && e.KeyCode == Keys.F)
        {
            ShowFindBox();
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            CopyText();
        }

        if (!enableIntellisense) return;

        if (e.KeyCode == Keys.Down)
            if (panelWords.Visible && !lvWords.Focused)
            {
                lvWords.Focus();

                if (lvWords.Items.Count > 0) lvWords.Items[0].Selected = true;

                e.SuppressKeyPress = true;
            }
    }

    private void ShowFindBox()
    {
        if (findBox == null || findBox.IsDisposed)
        {
            findBox = new frmFindBox(true);

            findBox.OnFind += FindBox_OnFind;
            findBox.OnEndFind += FindBox_OnEndFind;
        }

        findBox.StartPosition = FormStartPosition.Manual;

        var topControl = GetTopConrol();

        if (topControl != null)
            findBox.Location = new Point(topControl.Left + topControl.Width - findBox.Width - 40, topControl.Top + 130);
        else
            findBox.Location = new Point(1000, 150);

        findBox.Show();
    }

    private void FindBox_OnEndFind()
    {
        ClearSelection();
    }

    private Control GetTopConrol()
    {
        var parent = Parent;

        while (parent != null)
        {
            if (parent.Parent == null) return parent;

            parent = parent.Parent;
        }

        return null;
    }

    private void FindBox_OnFind()
    {
        RichTextBoxHelper.HighlightingFindWord(Editor, findBox.FindWord, findBox.MatchCase, findBox.MatchWholeWord);
    }

    private void txtEditor_KeyUp(object sender, KeyEventArgs e)
    {
        ShowCurrentPosition();

        if (isPasting) return;

        try
        {
            HandleKeyUpForIntellisense(e);
        }
        catch (Exception ex)
        {
        }
    }

    private void HandleKeyUpForIntellisense(KeyEventArgs e)
    {
        if (e.KeyValue >= 112 && e.KeyValue <= 123)
        {
            SetWordListViewVisible(false);
            return;
        }

        if (e.KeyCode == Keys.Space)
        {
            ClearStyleForSpace();

            SetWordListViewVisible(false);
        }

        var token = GetLastWordToken();

        if (token == null || token.Text == null || token.Type != SqlWordTokenType.None)
        {
            SetWordListViewVisible(false);

            if (token != null && token.Text != null)
                if (token.Type != SqlWordTokenType.String && token.Text.Contains("'"))
                    ClearStyle(token);
        }

        if (enableIntellisense && panelWords.Visible)
            if (lvWords.Tag is SqlWord word)
            {
                if (word.Type == SqlWordTokenType.Table)
                {
                    string columnName = null;

                    var index = Editor.SelectionStart;
                    var c = Editor.Text[index - 1];

                    if (c != '.') columnName = token.Text;

                    ShowTableColumns(word.Text, columnName);
                }
                else if (word.Type == SqlWordTokenType.Schema)
                {
                    ShowDbObjects(token.Text);
                }

                return;
            }

        if (e.KeyData == Keys.OemPeriod)
        {
            if (enableIntellisense)
            {
                if (token.Type == SqlWordTokenType.String) return;

                var word = FindWord(token.Text);

                if (word.Type == SqlWordTokenType.Table)
                {
                    ShowTableColumns(word.Text);
                    lvWords.Tag = word;
                }
                else if (word.Type == SqlWordTokenType.Schema)
                {
                    ShowDbObjects(null, word.Text);
                    lvWords.Tag = word;
                }
            }
        }
        else if (e.KeyCode == Keys.Back)
        {
            if (enableIntellisense && panelWords.Visible)
            {
                if (!IsMatchWord(token.Text) || Editor.Text.Length == 0)
                    SetWordListViewVisible(false);
                else
                    ShowWordListByToken(token);
            }

            if (token != null && token.Text.Length > 0 && commentString.Contains(token.Text.Last()))
                HighlightingWord(token);
        }
        else if (e.KeyValue < 48 || (e.KeyValue >= 58 && e.KeyValue <= 64) || (e.KeyValue >= 91 && e.KeyValue <= 96) ||
                 e.KeyValue > 122)
        {
            SetWordListViewVisible(false);
        }
        else
        {
            if (enableIntellisense)
                if (!IsWordInQuotationChar(token))
                    ShowWordListByToken(token);
        }
    }

    private bool IsWordInQuotationChar(SqlWordToken token)
    {
        if (token == null) return false;

        var startIndex = token.StopIndex;

        if (startIndex == 0) return false;

        var singleQotationCharCount = Editor.Text.Substring(0, startIndex).Count(item => item == '\'');

        return singleQotationCharCount % 2 != 0;
    }

    private void HighlightingWord(SqlWordToken token)
    {
        var start = Editor.SelectionStart;
        var lineIndex = Editor.GetLineFromCharIndex(start);
        var stop = Editor.GetFirstCharIndexFromLine(lineIndex) + Editor.Lines[lineIndex].Length - 1;

        RichTextBoxHelper.Highlighting(Editor, DatabaseType, true, start, stop);
        ;
    }

    private void ShowWordListByToken(SqlWordToken token)
    {
        if (token == null || string.IsNullOrEmpty(token.Text) || token.Type == SqlWordTokenType.Number)
        {
            SetWordListViewVisible(false);

            return;
        }

        var type = DetectTypeByWord(token.Text);

        var words = SqlWordFinder.FindWords(DatabaseType, token.Text, type);

        ShowWordList(words);
    }

    private void ShowTableColumns(string tableName, string columnName = null)
    {
        IEnumerable<SqlWord> columns =
            SqlWordFinder.FindWords(DatabaseType, columnName, SqlWordTokenType.TableColumn, tableName);

        ShowWordList(columns);
    }

    private void ShowDbObjects(string search, string owner = null)
    {
        IEnumerable<SqlWord> words = SqlWordFinder.FindWords(DatabaseType, search,
            SqlWordTokenType.Table | SqlWordTokenType.View | SqlWordTokenType.Function, owner);

        if (!string.IsNullOrEmpty(search))
        {
            var sortedWords = new List<SqlWord>();

            sortedWords.AddRange(words.Where(item => item.Text.StartsWith(search, StringComparison.OrdinalIgnoreCase)));
            sortedWords.AddRange(words.Where(item =>
                !item.Text.StartsWith(search, StringComparison.OrdinalIgnoreCase)));

            ShowWordList(sortedWords);
        }
        else
        {
            ShowWordList(words);
        }
    }

    private void ShowWordList(IEnumerable<SqlWord> words)
    {
        if (words.Count() > 0)
        {
            lvWords.Items.Clear();

            foreach (var sw in words)
            {
                var item = new ListViewItem();

                switch (sw.Type)
                {
                    case SqlWordTokenType.Keyword:
                        item.ImageIndex = 0;
                        break;
                    case SqlWordTokenType.BuiltinFunction:
                    case SqlWordTokenType.Function:
                        item.ImageIndex = 1;
                        break;
                    case SqlWordTokenType.Table:
                        item.ImageIndex = 2;
                        break;
                    case SqlWordTokenType.View:
                        item.ImageIndex = 3;
                        break;
                    case SqlWordTokenType.TableColumn:
                        item.ImageIndex = 4;
                        break;
                    case SqlWordTokenType.Schema:
                        item.ImageIndex = 5;
                        break;
                }

                item.SubItems.Add(sw.Text);
                item.SubItems[1].Tag = sw.Type;
                item.Tag = sw.Source;

                lvWords.Items.Add(item);
            }

            var longestText = words.OrderByDescending(item => item.Text.Length).FirstOrDefault().Text;

            var width = MeasureTextWidth(lvWords, longestText);

            lvWords.Columns[1].Width = width + 20;

            var totalWidth = lvWords.Columns.Cast<ColumnHeader>().Sum(item => item.Width) + 50;

            panelWords.Width = totalWidth < WordListMinWidth ? WordListMinWidth : totalWidth;

            SetWordListPanelPostition();

            SetWordListViewVisible(true);
        }
        else
        {
            SetWordListViewVisible(false);
        }
    }

    private void SetWordListPanelPostition()
    {
        var point = Editor.GetPositionFromCharIndex(Editor.SelectionStart);
        point.Y += (int)Math.Ceiling(Editor.Font.GetHeight()) + 2;
        point.X += 2;

        panelWords.Location = point;
    }

    private void ClearStyle(SqlWordToken token)
    {
        Editor.Select(token.StartIndex, token.StopIndex - token.StartIndex + 1);
        Editor.SelectionColor = Color.Black;
        Editor.SelectionStart = token.StopIndex + 1;
        Editor.SelectionLength = 0;
    }

    private void ClearStyleForSpace()
    {
        var start = Editor.SelectionStart;
        Editor.Select(start - 1, 1);
        Editor.SelectionColor = Color.Black;
        Editor.SelectionStart = start;
        Editor.SelectionLength = 0;
    }

    private SqlWordTokenType DetectTypeByWord(string word)
    {
        switch (word.ToUpper())
        {
            case "FROM":
                return SqlWordTokenType.Table | SqlWordTokenType.View;
        }

        return SqlWordTokenType.None;
    }

    private bool IsMatchWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        var words = SqlWordFinder.FindWords(DatabaseType, word);

        return words.Count > 0;
    }

    private void SetWordListViewVisible(bool visible)
    {
        if (visible)
        {
            panelWords.BringToFront();
            panelWords.Show();
        }
        else
        {
            txtToolTip.Hide();
            panelWords.Hide();
            lvWords.Tag = null;
        }
    }

    private SqlWordToken GetLastWordToken(bool noAction = false, bool isInsert = false)
    {
        SqlWordToken token = null;

        var currentIndex = Editor.SelectionStart;
        var lineIndex = Editor.GetLineFromCharIndex(currentIndex);
        var lineFirstCharIndex = Editor.GetFirstCharIndexOfCurrentLine();

        var index = currentIndex - 1;

        if (index < 0 || index > Editor.Text.Length - 1) return token;

        token = new SqlWordToken();

        var isDot = false;

        if (Editor.Text[index] == '.')
        {
            isDot = true;

            if (isInsert)
            {
                token.StartIndex = token.StopIndex = Editor.SelectionStart;
                token.Text = ".";

                return token;
            }

            index = index - 1;
        }

        token.StopIndex = index;

        var lineBefore = Editor.Text.Substring(lineFirstCharIndex, currentIndex - lineFirstCharIndex);

        var isComment = false;

        if (DbInterpreter != null && lineBefore.Contains(commentString)) isComment = true;

        var word = "";

        if (!isComment)
        {
            var chars = new List<char>();

            var delimeterPattern = @"[ ,\.\r\n=]";

            var i = -1;

            var existed = false;
            for (i = index; i >= 0; i--)
            {
                var c = Editor.Text[i];

                if (!Regex.IsMatch(c.ToString(), delimeterPattern))
                {
                    chars.Add(c);

                    if (c == '\'') break;

                    if (c == '(')
                    {
                        if (chars.Count > 1)
                        {
                            chars.RemoveAt(chars.Count - 1);
                            i++;
                        }

                        break;
                    }
                }
                else
                {
                    existed = true;
                    break;
                }
            }

            if (i == -1) i = 0;

            chars.Reverse();

            word = string.Join("", chars);

            token.Text = word;

            token.StartIndex = i + (existed ? 1 : 0);

            if (token.StartIndex == token.StopIndex && isInsert && word.Length > 0)
                token.StopIndex = token.StartIndex + word.Length;

            if (word.Contains("'"))
            {
                var singQuotationCount = lineBefore.Count(item => item == '\'');

                var isQuotationPaired = singQuotationCount % 2 == 0;

                if (isQuotationPaired && word.StartsWith("'"))
                {
                    var afterChars = new List<char>();

                    for (var j = currentIndex; j < Editor.Text.Length; j++)
                    {
                        var c = Editor.Text[j];

                        if (Regex.IsMatch(c.ToString(), delimeterPattern))
                            break;
                        afterChars.Add(c);
                    }

                    var afterWord = string.Join("", afterChars);

                    if (afterWord.EndsWith("'") || (word == "'" && afterChars.Count == 0))
                    {
                        token.Type = SqlWordTokenType.String;
                    }
                    else
                    {
                        token.StartIndex++;
                        token.Text = token.Text.Substring(1);
                    }
                }
                else if (!isQuotationPaired || (isQuotationPaired && word.EndsWith("'")))
                {
                    token.Type = SqlWordTokenType.String;
                }

                if (token.Type == SqlWordTokenType.String)
                {
                    if (!isDot) SetWordColor(token);

                    return token;
                }
            }
        }
        else
        {
            var firstIndexOfComment = lineFirstCharIndex + lineBefore.IndexOf(commentString, StringComparison.Ordinal);

            token.StartIndex = firstIndexOfComment;
            token.StopIndex = lineFirstCharIndex + Editor.Lines[lineIndex].Length - 1;
        }

        var trimedWord = TrimQuotationChars(word);

        if (!noAction)
        {
            if (enableIntellisense && dbSchemas.Any(item => item.ToUpper() == trimedWord.ToUpper()))
            {
                token.Type = SqlWordTokenType.Schema;
            }
            else if (keywords.Any(item => item.ToUpper() == word.ToUpper()))
            {
                token.Type = SqlWordTokenType.Keyword;

                SetWordColor(token);
            }
            else if (builtinFunctions.Any(item => item.Name.ToUpper() == trimedWord.ToUpper()))
            {
                token.Type = SqlWordTokenType.BuiltinFunction;

                SetWordColor(token);
            }
            else if (isComment)
            {
                token.Type = SqlWordTokenType.Comment;

                SetWordColor(token, true);
            }
            else if (long.TryParse(word, out _))
            {
                token.Type = SqlWordTokenType.Number;
            }
            else
            {
                if (!isDot && !IsWordInQuotationChar(token)) ClearStyle(token);
            }
        }

        return token;
    }

    private SqlWord FindWord(string text)
    {
        text = TrimQuotationChars(text);

        SqlWord word = null;

        if (dbSchemas.Count > 0 && dbSchemas.Any(item => text.ToUpper() == item.ToUpper()))
        {
            word = new SqlWord { Type = SqlWordTokenType.Schema, Text = text };

            return word;
        }

        word = allWords.FirstOrDefault(item => item.Text.ToUpper() == text.ToUpper()
                                               && (item.Type == SqlWordTokenType.Table ||
                                                   item.Type == SqlWordTokenType.View));

        if (word != null)
            return word;
        word = new SqlWord { Text = text };

        var quotationLeftChar = DbInterpreter.QuotationLeftChar;
        var quotationRightChar = DbInterpreter.QuotationRightChar;

        var quotationNamePattern = $@"([{quotationLeftChar}]{nameWithSpacePattern}[{quotationRightChar}])";

        var regex = new Regex($@"({namePattern}|{quotationNamePattern})[\s\n\r]+(AS[\s\n\r]+)?\b({text})\b",
            RegexOptions.IgnoreCase);

        var matches = regex.Matches(Editor.Text);

        var name = "";
        foreach (Match match in matches)
            if (match.Value.Trim().ToUpper() != text.ToUpper())
            {
                var lastIndexOfSpace = match.Value.LastIndexOf(' ');

                var value = Regex.Replace(match.Value.Substring(0, lastIndexOfSpace), @" AS[\s\n\r]?", "",
                    RegexOptions.IgnoreCase).Trim();

                if (!keywords.Any(item => item.ToUpper() == value.ToUpper()))
                {
                    name = TrimQuotationChars(value);
                    break;
                }
            }

        if (string.IsNullOrEmpty(name)) name = text;

        if (schemaInfo.Tables.Any(item => item.Name.ToUpper() == name.ToUpper()))
        {
            word.Text = name;
            word.Type = SqlWordTokenType.Table;
        }

        return word;
    }

    private string TrimQuotationChars(string value)
    {
        if (DbInterpreter != null)
            return value.Trim(DbInterpreter.QuotationLeftChar, DbInterpreter.QuotationRightChar, '"');

        return value;
    }

    private void SetWordColor(SqlWordToken token, bool keepCurrentPos = false)
    {
        if (!SettingManager.Setting.EnableEditorHighlighting) return;

        var color = Color.Black;

        if (token.Type == SqlWordTokenType.Keyword)
            color = Color.Blue;
        else if (token.Type == SqlWordTokenType.BuiltinFunction)
            color = ColorTranslator.FromHtml("#FF00FF");
        else if (token.Type == SqlWordTokenType.String)
            color = Color.Red;
        else if (token.Type == SqlWordTokenType.Comment) color = ColorTranslator.FromHtml("#008000");

        var start = Editor.SelectionStart;

        Editor.Select(token.StartIndex, token.StopIndex - token.StartIndex + 1);
        Editor.SelectionBackColor = Editor.BackColor;
        Editor.SelectionColor = color;
        Editor.SelectionStart = keepCurrentPos ? start : token.StopIndex + 1;
        Editor.SelectionLength = 0;
    }

    private void InsertSelectedWord()
    {
        try
        {
            var token = GetLastWordToken(true, true);

            var item = lvWords.SelectedItems[0];
            var tag = item.Tag;

            var selectedWord = item.SubItems[1].Text;

            var length = token.StartIndex == token.StopIndex ? 0 : token.StopIndex - token.StartIndex + 1;

            Editor.Select(token.StartIndex, length);

            var quotationValue = selectedWord;

            if (!(tag is FunctionSpecification)) quotationValue = DbInterpreter.GetQuotedString(selectedWord);

            Editor.SelectedText = quotationValue;

            SetWordListViewVisible(false);

            Editor.SelectionStart = Editor.SelectionStart;
            Editor.Focus();
        }
        catch (Exception ex)
        {
        }
    }

    private void ShowCurrentPosition()
    {
        var message = "";

        if (Editor.SelectionStart >= 0)
        {
            var lineIndex = Editor.GetLineFromCharIndex(Editor.SelectionStart);
            var column = Editor.SelectionStart - Editor.GetFirstCharIndexOfCurrentLine() + 1;

            message = $"Line:{lineIndex + 1}  Column:{column} Index:{Editor.SelectionStart}";
        }
        else
        {
            message = "";
        }

        OnQueryEditorInfoMessage?.Invoke(message);
    }

    private void lvWords_DoubleClick(object sender, EventArgs e)
    {
        if (lvWords.SelectedItems.Count > 0) InsertSelectedWord();
    }

    private void lvWords_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
            InsertSelectedWord();
        else if (!(e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            if (panelWords.Visible)
            {
                panelWords.Visible = false;
                Editor.SelectionStart = Editor.SelectionStart;
                Editor.Focus();
            }
    }

    private void tsmiDisableIntellisense_Click(object sender, EventArgs e)
    {
        if (enableIntellisense)
        {
            enableIntellisense = false;
            intellisenseSetuped = false;
        }
        else
        {
            if (!intellisenseSetuped)
            {
                SetupIntellisenseRequired?.Invoke(this, null);
            }
        }
    }

    private void txtEditor_SelectionChanged(object sender, EventArgs e)
    {
        if (isPasting)
        {
            isPasting = false;

            RichTextBoxHelper.Highlighting(Editor, DatabaseType);
        }
    }

    private void lvWords_SelectedIndexChanged(object sender, EventArgs e)
    {
        txtToolTip.Visible = false;

        if (lvWords.SelectedItems.Count > 0)
        {
            var item = lvWords.SelectedItems[0];

            var source = item.Tag;
            string tooltip = null;

            if (source is FunctionSpecification funcSpec)
                tooltip = $"{funcSpec.Name}({funcSpec.Args})";
            else if (source is TableColumn column) tooltip = $"{column.Name}({DbInterpreter.ParseDataType(column)})";

            if (!string.IsNullOrEmpty(tooltip)) ShowTooltip(tooltip, item);
        }
    }

    private void ShowTooltip(string text, ListViewItem item)
    {
        txtToolTip.Text = text;

        txtToolTip.Location =
            new Point(panelWords.Location.X + panelWords.Width, panelWords.Location.Y + item.Position.Y);

        txtToolTip.Width = MeasureTextWidth(txtToolTip, text);

        txtToolTip.Visible = true;
    }

    private int MeasureTextWidth(Control control, string text)
    {
        using (var g = CreateGraphics())
        {
            return (int)Math.Ceiling(g.MeasureString(text, control.Font).Width);
        }
    }

    private void tsmiUpdateIntellisense_Click(object sender, EventArgs e)
    {
        SetupIntellisenseRequired?.Invoke(this, null);
    }

    private void txtEditor_MouseClick(object sender, MouseEventArgs e)
    {
        HandleMouseDownClick(e);
    }

    private void txtEditor_MouseDown(object sender, MouseEventArgs e)
    {
        HandleMouseDownClick(e);
    }

    private void HandleMouseDownClick(MouseEventArgs e)
    {
        ShowCurrentPosition();

        isPasting = false;

        if (!enableIntellisense) return;

        txtToolTip.Visible = false;

        if (panelWords.Visible && !panelWords.Bounds.Contains(e.Location))
        {
            panelWords.Visible = false;
            lvWords.Items.Clear();
            lvWords.Tag = null;
        }
    }

    private void tsmiSelectAll_Click(object sender, EventArgs e)
    {
        Editor.SelectAll();
    }

    private void tsmiValidateScripts_Click(object sender, EventArgs e)
    {
        ClearSelection();

        ValidateScripts(true);
    }

    internal async void ValidateScripts(bool showMessageBox = false)
    {
        var error = await Task.Run(() => ScriptValidator.ValidateSyntax(DatabaseType, Editor.Text));

        if (error != null && error.HasError)
        {
            if (showMessageBox)
            {
                var msgBox = new frmTextContent("Error Message", error.ToString(), true);
                msgBox.ShowDialog();
            }

            RichTextBoxHelper.HighlightingError(Editor, error);
        }
        else
        {
            if (showMessageBox) MessageBox.Show("The scripts is valid.");
        }
    }

    private void ClearSelection()
    {
        var start = Editor.SelectionStart;

        Editor.SelectAll();
        Editor.SelectionBackColor = Color.White;
        Editor.SelectionStart = start;
        Editor.SelectionLength = 0;
    }

    private void editorContexMenu_Opening(object sender, CancelEventArgs e)
    {
        var hasText = Editor.Text.Trim().Length > 0;
        tsmiValidateScripts.Visible = hasText;
        tsmiFindText.Visible = hasText;
    }

    internal void DisposeResources()
    {
        if (findBox != null && !findBox.IsDisposed)
        {
            findBox.Close();
            findBox.Dispose();
        }
    }

    private void tsmiFindText_Click(object sender, EventArgs e)
    {
        ShowFindBox();
    }
}