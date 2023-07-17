using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Controls;

public partial class UC_DbObjectContent : UserControl
{
    private readonly Dictionary<int, Rectangle> dictCloseButtonRectangle = new();

    public DataFilterHandler OnDataFilter;
    public FeedbackHandler OnFeedback;

    public UC_DbObjectContent()
    {
        InitializeComponent();

        CheckForIllegalCrossThreadCalls = false;
        CheckForIllegalCrossThreadCalls = false;

        FormEventCenter.OnSave += Save;
        FormEventCenter.OnRunScripts += RunScripts;
    }

    public void ShowContent(DatabaseObjectDisplayInfo info)
    {
        Visible = true;

        var page = FindTabPage(info);

        var title = GetFormatTabHeaderText(GetInfoName(info));

        if (page == null)
        {
            page = new TabPage(title);

            tabControl1.TabPages.Insert(0, page);

            tabControl1.SelectedTab = page;
        }
        else
        {
            tabControl1.SelectedTab = page;
        }

        page.Tag = info;

        page.BackColor = Color.Transparent;

        SetTabPageContent(info, page);

        SetTabPageTooltip(page);
    }

    private string GetFormatTabHeaderText(string text)
    {
        return $" {text}  ";
    }

    private void SetTabPageTooltip(TabPage page)
    {
        var info = page.Tag as DatabaseObjectDisplayInfo;

        if (info != null)
        {
            var database = info.ConnectionInfo == null
                ? ""
                : $@": {info.ConnectionInfo?.Server}-{info.ConnectionInfo?.Database}";

            var filePath = File.Exists(info.FilePath) ? info.FilePath + "  -  " : "";

            var title = string.IsNullOrEmpty(filePath) ? $" - {page.Text}" : "";

            page.ToolTipText = $@"{filePath}{info.DatabaseType}{database}{title}";
        }
    }

    private void SetTabPageContent(DatabaseObjectDisplayInfo info, TabPage tabPage)
    {
        if (info.DisplayType == DatabaseObjectDisplayType.Script)
        {
            var sqlQuery = GetUcControl<UC_SqlQuery>(tabPage);

            if (sqlQuery == null) sqlQuery = AddControlToTabPage<UC_SqlQuery>(tabPage);

            sqlQuery.Show(info);

            if (!string.IsNullOrEmpty(sqlQuery.Editor.Text))
            {
                RichTextBoxHelper.Highlighting(sqlQuery.Editor, info.DatabaseType, false);

                if (info.Error != null) RichTextBoxHelper.HighlightingError(sqlQuery.Editor, info.Error);

                if (info.DatabaseObject is ScriptDbObject && SettingManager.Setting.ValidateScriptsAfterTranslated &&
                    info.IsTranlatedScript) sqlQuery.ValidateScripts();
            }
            else
            {
                sqlQuery.Editor.Focus();
            }
        }
        else if (info.DisplayType == DatabaseObjectDisplayType.Data)
        {
            var dataViewer = GetUcControl<UC_DataViewer>(tabPage);

            if (dataViewer == null)
            {
                dataViewer = AddControlToTabPage<UC_DataViewer>(tabPage);
                dataViewer.OnDataFilter += DataFilter;
            }

            dataViewer.Show(info);
        }
        else if (info.DisplayType == DatabaseObjectDisplayType.TableDesigner)
        {
            var tableDesigner = GetUcControl<UC_TableDesigner>(tabPage);

            if (tableDesigner == null)
            {
                tableDesigner = AddControlToTabPage<UC_TableDesigner>(tabPage);
                tableDesigner.OnFeedback += Feedback;
            }

            tableDesigner.Show(info);
        }
    }

    private void DataFilter(object sender)
    {
        if (OnDataFilter != null) OnDataFilter(sender);
    }

    private T AddControlToTabPage<T>(TabPage tabPage) where T : UserControl
    {
        var control = (T)Activator.CreateInstance(typeof(T));
        control.Dock = DockStyle.Fill;
        tabPage.Controls.Add(control);

        return control;
    }

    private T GetUcControl<T>(TabPage tabPage) where T : UserControl
    {
        foreach (Control control in tabPage.Controls)
            if (control is T)
                return control as T;

        return null;
    }

    public TabPage FindTabPage(DatabaseObjectDisplayInfo displayInfo)
    {
        foreach (TabPage page in tabControl1.TabPages)
        {
            var data = page.Tag as DatabaseObjectDisplayInfo;

            if (data.Name == displayInfo.Name && displayInfo.DatabaseType == data.DatabaseType &&
                displayInfo.DisplayType == data.DisplayType) return page;
        }

        return null;
    }

    private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index >= tabControl1.TabPages.Count) return;

        var imgClose = Resources.TabClose;

        var headerCloseButtonSize = imgClose.Size.Width;

        var isSelected = e.Index == tabControl1.SelectedIndex;

        var backBrush = new SolidBrush(isSelected ? Color.White : ColorTranslator.FromHtml("#DEE1E6"));

        var headerRect = tabControl1.GetTabRect(e.Index);

        e.Graphics.FillRectangle(backBrush, headerRect);

        var font = new Font(Font, isSelected ? FontStyle.Bold : FontStyle.Regular);

        Brush fontBrush = new SolidBrush(Color.Black);

        e.Graphics.DrawString(tabControl1.TabPages[e.Index].Text.TrimEnd(), font, fontBrush, headerRect.X,
            headerRect.Y + 2);

        var closeButtonRect = new Rectangle(headerRect.X + headerRect.Width - headerCloseButtonSize, headerRect.Y + 2,
            headerCloseButtonSize, headerCloseButtonSize);

        e.Graphics.DrawImage(imgClose, closeButtonRect);

        if (!dictCloseButtonRectangle.ContainsKey(e.Index))
            dictCloseButtonRectangle.Add(e.Index, closeButtonRect);
        else
            dictCloseButtonRectangle[e.Index] = closeButtonRect;

        e.Graphics.Dispose();
    }

    private int FindTabPageIndex(int x, int y)
    {
        foreach (var kp in dictCloseButtonRectangle)
        {
            var rect = kp.Value;

            if (x >= rect.X && x <= rect.X + rect.Width
                            && y >= rect.Y && y <= rect.Y + rect.Height)
                return kp.Key;
        }

        return -1;
    }

    private void tabControl1_MouseClick(object sender, MouseEventArgs e)
    {
        var tabPageIndex = FindTabPageIndex(e.X, e.Y);

        if (tabPageIndex >= 0) CloseTabPage(tabPageIndex);
    }

    private void tabControl1_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
            for (var i = 0; i < tabControl1.TabPages.Count; i++)
            {
                var r = tabControl1.GetTabRect(i);

                if (r.Contains(e.Location))
                {
                    tabControl1.SelectedIndex = i;

                    SetMenuItemsVisible();

                    scriptContentMenu.Show(this, e.Location);

                    break;
                }
            }
    }

    private void SetMenuItemsVisible()
    {
        tsmiCloseOthers.Visible = tabControl1.TabPages.Count > 1;
        tsmiCloseAll.Visible = tabControl1.TabPages.Count > 1;
    }

    private async void tsmiClose_Click(object sender, EventArgs e)
    {
        if (tabControl1.SelectedIndex >= 0) await CloseTabPage(tabControl1.SelectedIndex);

        SetControlVisible();
    }

    private async Task<bool> CloseTabPage(int tabPageIndex)
    {
        var canClose = true;

        var tabPage = tabControl1.TabPages[tabPageIndex];

        var info = tabPage.Tag as DatabaseObjectDisplayInfo;

        var isNew = info.IsNew;

        if (info != null)
        {
            var control = GetUcControlInterface(tabPage);

            var saveRequired = false;

            if (control is UC_SqlQuery sqlQuery)
            {
                if (isNew)
                {
                    if (sqlQuery.Editor.Text.Trim().Length > 0) saveRequired = true;
                }
                else
                {
                    if (sqlQuery.IsTextChanged()) saveRequired = true;
                }
            }
            else if (control is UC_TableDesigner tableDesigner)
            {
                if (await tableDesigner.IsChanged()) saveRequired = true;
            }

            if (saveRequired)
            {
                var result = MessageBox.Show($"Do you want to save {info.Name}?", "Confirm",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                    Save();
                else if (result == DialogResult.Cancel) canClose = false;
            }
        }

        if (canClose)
        {
            if (info.DisplayType == DatabaseObjectDisplayType.Script)
            {
                var sqlQueryControl = GetUcControlInterface(tabPage) as UC_SqlQuery;

                sqlQueryControl.DisposeResources();
            }

            tabControl1.TabPages.RemoveAt(tabPageIndex);
            dictCloseButtonRectangle.Remove(tabPageIndex);
        }

        SetControlVisible();

        return canClose;
    }

    private async void tsmiCloseOthers_Click(object sender, EventArgs e)
    {
        dictCloseButtonRectangle.Clear();

        var index = tabControl1.SelectedIndex;

        for (var i = tabControl1.TabPages.Count - 1; i >= index + 1; i--) await CloseTabPage(i);

        while (tabControl1.TabPages.Count > 1) await CloseTabPage(0);

        SetControlVisible();
    }

    private void SetControlVisible()
    {
        Visible = tabControl1.TabPages.Count > 0;
    }

    private async void tsmiCloseAll_Click(object sender, EventArgs e)
    {
        for (var i = tabControl1.TabCount - 1; i >= 0; i--) await CloseTabPage(i);

        SetControlVisible();
    }

    private void tsmiSave_Click(object sender, EventArgs e)
    {
        Save();
    }

    private void Save()
    {
        if (tabControl1.TabCount < 1) return;

        var tabPage = tabControl1.SelectedTab;

        if (tabPage == null) return;

        var displayInfo = tabPage.Tag as DatabaseObjectDisplayInfo;

        if (displayInfo == null) return;

        var displayType = displayInfo.DisplayType;

        if (displayType == DatabaseObjectDisplayType.Script || displayType == DatabaseObjectDisplayType.Data)
        {
            if (File.Exists(displayInfo.FilePath))
            {
                SaveToFile(tabPage, displayInfo.FilePath);
                return;
            }

            if (dlgSave == null) dlgSave = new SaveFileDialog();

            dlgSave.FileName = tabPage.Text.Trim();

            if (displayType == DatabaseObjectDisplayType.Script)
                dlgSave.Filter = "sql file|*.sql|txt file|*.txt";
            else if (displayType == DatabaseObjectDisplayType.Data) dlgSave.Filter = "csv file|*.csv|txt file|*.txt";

            var result = dlgSave.ShowDialog();

            if (result == DialogResult.OK)
            {
                var filePath = dlgSave.FileName;

                SaveToFile(tabPage, filePath);

                displayInfo.FilePath = filePath;

                var name = Path.GetFileNameWithoutExtension(filePath);

                displayInfo.IsNew = false;
                displayInfo.Name = name;

                tabPage.Text = GetFormatTabHeaderText(name);

                SetTabPageTooltip(tabPage);
            }
        }
        else if (displayType == DatabaseObjectDisplayType.TableDesigner)
        {
            var tableDesigner = GetUcControl<UC_TableDesigner>(tabPage);
            var result = tableDesigner.Save(new ContentSaveInfo());

            if (result.IsOK)
            {
                var table = result.ResultData as Table;

                displayInfo.IsNew = false;
                displayInfo.Name = table.Name;

                tabPage.Text = GetFormatTabHeaderText(GetInfoName(displayInfo));

                SetTabPageTooltip(tabPage);
            }
        }
    }

    private void SaveToFile(TabPage tabPage, string filePath)
    {
        var control = GetUcControlInterface(tabPage);

        if (control != null) control.Save(new ContentSaveInfo { FilePath = filePath });
    }

    private IDbObjContentDisplayer GetUcControlInterface(TabPage tabPage)
    {
        foreach (Control control in tabPage.Controls)
            if (control is IDbObjContentDisplayer)
                return control as IDbObjContentDisplayer;

        return null;
    }

    private string GetInfoName(DatabaseObjectDisplayInfo info)
    {
        var name = info.Name;
        var isOpenFile = !string.IsNullOrEmpty(info.FilePath);

        if (isOpenFile) return name;

        if (string.IsNullOrEmpty(name))
        {
            if (info.IsNew)
            {
                var prefix = "";

                if (info.DisplayType == DatabaseObjectDisplayType.Script)
                    prefix = "SQLQuery";
                else if (info.DisplayType == DatabaseObjectDisplayType.TableDesigner) prefix = "Table";

                var num = GetNewMaxNameNumber(prefix);

                name = prefix + (num + 1);

                info.Name = name;
            }
        }
        else
        {
            if (info.DatabaseType == DatabaseType.SqlServer || info.DatabaseType == DatabaseType.Postgres)
            {
                var dbObject = info.DatabaseObject;

                if (dbObject != null)
                {
                    var schema = info.Schema ?? dbObject.Schema;

                    if (!string.IsNullOrEmpty(schema))
                        name = $"{schema}.{dbObject.Name}";
                    else
                        name = dbObject.Name;
                }
            }
        }

        return name;
    }

    private int GetNewMaxNameNumber(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return 0;

        var names = new List<string>();
        foreach (TabPage page in tabControl1.TabPages)
        {
            var data = page.Tag as DatabaseObjectDisplayInfo;

            if (data.Name.Trim().StartsWith(prefix)) names.Add(data.Name.Trim());
        }

        var maxName = names.OrderByDescending(item => item.Length).ThenByDescending(item => item).FirstOrDefault();

        var num = 0;

        if (!string.IsNullOrEmpty(maxName))
        {
            var strNum = maxName.Replace(prefix, "");

            if (int.TryParse(strNum, out num))
            {
            }
        }

        return num;
    }

    public void RunScripts()
    {
        if (tabControl1.TabCount == 0) return;

        var tabPage = tabControl1.SelectedTab;

        if (tabPage == null) return;

        var data = tabPage.Tag as DatabaseObjectDisplayInfo;

        if (data == null || data.DisplayType != DatabaseObjectDisplayType.Script) return;

        var sqlQuery = GetUcControl<UC_SqlQuery>(tabPage);

        sqlQuery.RunScripts(data);
    }

    private void tabControl1_MouseHover(object sender, EventArgs e)
    {
        var tabPage = tabControl1.SelectedTab;

        if (tabPage != null) SetTabPageTooltip(tabPage);
    }

    private void Feedback(FeedbackInfo info)
    {
        if (OnFeedback != null) OnFeedback(info);
    }
}