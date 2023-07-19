using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrightIdeasSoftware;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace DatabaseManager;

public partial class frmCompare : Form, IObserver<FeedbackInfo>
{
    private List<DbDifference> differences;
    private bool isRichTextBoxScrolling;
    private readonly DatabaseType sourceDatabaseType;
    private ConnectionInfo sourceDbConnectionInfo;
    private DbInterpreter sourceInterpreter;
    private SchemaInfo sourceSchemaInfo;
    private DbScriptGenerator sourceScriptGenerator;
    private ConnectionInfo targetDbConnectionInfo;
    private DbInterpreter targetInterpreter;
    private SchemaInfo targetSchemaInfo;
    private DbScriptGenerator targetScriptGenerator;
    private readonly bool useSourceConnector = true;

    public frmCompare()
    {
        InitializeComponent();
    }

    public frmCompare(DatabaseType sourceDatabaseType, ConnectionInfo sourceConnectionInfo)
    {
        InitializeComponent();

        this.sourceDatabaseType = sourceDatabaseType;
        sourceDbConnectionInfo = sourceConnectionInfo;
        useSourceConnector = false;
    }

    private void frmCompare_Load(object sender, EventArgs e)
    {
        InitControls();

        if (!useSourceConnector)
        {
            targetDbProfile.DatabaseType = sourceDatabaseType;
            targetDbProfile.EnableDatabaseType = false;
        }
    }

    private void InitControls()
    {
        if (!useSourceConnector)
        {
            var increaseHeight = sourceDbProfile.Height;
            sourceDbProfile.Visible = false;
            btnCompare.Height = targetDbProfile.ClientHeight;
            targetDbProfile.Top -= increaseHeight;
            splitContainer1.Top -= increaseHeight;
            splitContainer1.Height += increaseHeight;
        }

        colType.ImageGetter = delegate(object x)
        {
            var difference = x as DbDifference;

            if (difference.DatabaseObjectType == DatabaseObjectType.None)
                return "tree_Folder.png";
            return $"tree_{difference.DatabaseObjectType}.png";
        };

        var treeColumnRenderer = tlvDifferences.TreeColumnRenderer;

        treeColumnRenderer.IsShowGlyphs = true;
        treeColumnRenderer.UseTriangles = true;

        var renderer = tlvDifferences.TreeColumnRenderer;
        renderer.LinePen = new Pen(Color.LightGray, 0.5f);
        renderer.LinePen.DashStyle = DashStyle.Dot;

        var differenceTypeRenderer = new FlagRenderer();

        differenceTypeRenderer.ImageList = imageList2;
        differenceTypeRenderer.Add(DbDifferenceType.Added, "Add.png");
        differenceTypeRenderer.Add(DbDifferenceType.Modified, "Edit.png");
        differenceTypeRenderer.Add(DbDifferenceType.Deleted, "Remove.png");

        colChangeType.Renderer = differenceTypeRenderer;

        colChangeType.ClusteringStrategy = new FlagClusteringStrategy(typeof(DbDifferenceType));

        tlvDifferences.Refresh();
    }

    private async void btnCompare_Click(object sender, EventArgs e)
    {
        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(targetDbProfile.DatabaseType, new ConnectionInfo(),
            new DbInterpreterOption());

        var checkItems =
            ItemsSelectorHelper.GetDatabaseObjectTypeItems(sourceDbProfile.DatabaseType,
                dbInterpreter.SupportDbObjectType);

        var selector = new frmItemsSelector("Select Database Object Types", checkItems);

        if (selector.ShowDialog() == DialogResult.OK)
        {
            tlvDifferences.Items.Clear();
            txtSource.Clear();
            txtTarget.Clear();

            var databaseObjectType = ItemsSelectorHelper.GetDatabaseObjectTypeByCheckItems(selector.CheckedItem);

            await Task.Run(() => Compare(databaseObjectType));
        }
    }

    private async void Compare(DatabaseObjectType databaseObjectType)
    {
        DatabaseType dbType;

        if (useSourceConnector)
        {
            dbType = sourceDbProfile.DatabaseType;

            if (!sourceDbProfile.IsDbTypeSelected())
            {
                MessageBox.Show("Please select a source database type.");
                return;
            }

            if (!sourceDbProfile.IsProfileSelected())
            {
                MessageBox.Show("Please select a source database profile.");
                return;
            }

            if (!sourceDbProfile.ValidateProfile()) return;
        }
        else
        {
            dbType = sourceDatabaseType;
        }

        if (sourceDbConnectionInfo == null)
        {
            MessageBox.Show("Source connection is null.");
            return;
        }

        if (targetDbConnectionInfo == null)
        {
            MessageBox.Show("Target connection info is null.");
            return;
        }

        if (dbType != targetDbProfile.DatabaseType)
        {
            MessageBox.Show("Target database type must be same as source database type.");
            return;
        }

        if (sourceDbConnectionInfo.Server == targetDbConnectionInfo.Server
            && sourceDbConnectionInfo.Port == targetDbConnectionInfo.Port
            && sourceDbConnectionInfo.Database == targetDbConnectionInfo.Database)
        {
            MessageBox.Show("Source database cannot be equal to the target database.");
            return;
        }

        if (!targetDbProfile.ValidateProfile()) return;

        btnCompare.Text = "...";
        btnCompare.Enabled = false;

        try
        {
            var sourceOption = new DbInterpreterOption();
            var targetOption = new DbInterpreterOption();
            var sourceFilter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };
            var targetFilter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };

            if (databaseObjectType.HasFlag(DatabaseObjectType.Table))
            {
                sourceOption.GetTableAllObjects = true;
                targetOption.GetTableAllObjects = true;
            }

            sourceInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, sourceDbConnectionInfo, sourceOption);
            targetInterpreter =
                DbInterpreterHelper.GetDbInterpreter(targetDbProfile.DatabaseType, targetDbConnectionInfo,
                    targetOption);
            sourceScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(sourceInterpreter);
            targetScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(targetInterpreter);

            sourceInterpreter.Subscribe(this);
            targetInterpreter.Subscribe(this);

            sourceSchemaInfo = await sourceInterpreter.GetSchemaInfoAsync(sourceFilter);
            targetSchemaInfo = await targetInterpreter.GetSchemaInfoAsync(targetFilter);

            var dbCompare = new DbCompare(sourceSchemaInfo, targetSchemaInfo);

            Feedback("Begin to compare...");

            differences = dbCompare.Compare();

            Feedback("End compare.");

            LoadData();
        }
        catch (Exception ex)
        {
            var message = ExceptionHelper.GetExceptionDetails(ex);

            LogHelper.LogError(message);

            MessageBox.Show("Error:" + message);
        }
        finally
        {
            btnCompare.Text = "Compare";
            btnCompare.Enabled = true;
        }
    }

    private void LoadData()
    {
        DecorateData(differences);

        tlvDifferences.CanExpandGetter = delegate(object obj)
        {
            var difference = obj as DbDifference;
            return CanExpand(difference);
        };

        tlvDifferences.ChildrenGetter = delegate(object obj)
        {
            var difference = obj as DbDifference;
            return GetChildren(difference);
        };

        var roots = differences.Where(item => item.DatabaseObjectType == DatabaseObjectType.None);

        tlvDifferences.Roots = roots;
    }

    private IEnumerable<DbDifference> GetChildren(DbDifference difference)
    {
        if (differences == null) return Enumerable.Empty<DbDifference>();

        var children = differences.Where(item => item.DifferenceType != DbDifferenceType.None &&
                                                 item.ParentType == difference.Type
                                                 && (item.ParentName == null ||
                                                     item.ParentName == difference.Source?.Name ||
                                                     item.ParentName == difference.Target?.Name ||
                                                     item.ParentName == null ||
                                                     item.ParentName == difference.Parent?.Source?.Name ||
                                                     item.ParentName == difference?.Parent.Target?.Name
                                                 ));

        if (difference.DatabaseObjectType == DatabaseObjectType.Table)
            return GetTableChildrenFolders(difference.Source as Table, difference.Target as Table, difference);
        if (difference.Type == DbObjectTreeFolderType.Columns.ToString())
            return difference.Parent.SubDifferences.Where(item => item.DatabaseObjectType == DatabaseObjectType.Column);
        if (difference.Type == "Primary Keys")
            return difference.Parent.SubDifferences.Where(item =>
                item.DatabaseObjectType == DatabaseObjectType.PrimaryKey);
        if (difference.Type == "Foreign Keys")
            return difference.Parent.SubDifferences.Where(item =>
                item.DatabaseObjectType == DatabaseObjectType.ForeignKey);
        if (difference.Type == DbObjectTreeFolderType.Indexes.ToString())
            return difference.Parent.SubDifferences.Where(item => item.DatabaseObjectType == DatabaseObjectType.Index);
        if (difference.Type == DbObjectTreeFolderType.Constraints.ToString())
            return difference.Parent.SubDifferences.Where(item =>
                item.DatabaseObjectType == DatabaseObjectType.Constraint);
        if (difference.Type == DbObjectTreeFolderType.Triggers.ToString())
            return difference.Parent.SubDifferences.Where(item =>
                item.DatabaseObjectType == DatabaseObjectType.Trigger);
        return children;
    }

    private bool CanExpand(DbDifference difference)
    {
        var databaseObjectType = difference.DatabaseObjectType;

        return databaseObjectType == DatabaseObjectType.None ||
               difference.DatabaseObjectType == DatabaseObjectType.Table;
    }

    private IEnumerable<DbDifference> GetTableChildrenFolders(Table source, Table target, DbDifference difference)
    {
        var tableName = source == null ? target.Name : source.Name;

        var differences = new List<DbDifference>();

        Action<DatabaseObjectType, string> addFolder = (databaseObjectType, folderName) =>
        {
            if (difference.SubDifferences.Any(item => item.DatabaseObjectType == databaseObjectType))
            {
                var differenceType = GetTableSubFolderDiffType(difference, databaseObjectType);

                if (differenceType != DbDifferenceType.None)
                    differences.Add(new DbDifference
                    {
                        Type = folderName, ParentType = nameof(Table), ParentName = tableName, Parent = difference,
                        DifferenceType = differenceType
                    });
            }
        };

        addFolder(DatabaseObjectType.Column, DbObjectTreeFolderType.Columns.ToString());

        addFolder(DatabaseObjectType.PrimaryKey, "Primary Keys");

        addFolder(DatabaseObjectType.ForeignKey, "Foreign Keys");

        addFolder(DatabaseObjectType.Index, DbObjectTreeFolderType.Indexes.ToString());

        addFolder(DatabaseObjectType.Constraint, DbObjectTreeFolderType.Constraints.ToString());

        addFolder(DatabaseObjectType.Trigger, DbObjectTreeFolderType.Triggers.ToString());

        return differences;
    }

    private DbDifferenceType GetTableSubFolderDiffType(DbDifference difference, DatabaseObjectType databaseObjectType)
    {
        return difference.SubDifferences.Any(item =>
            item.DatabaseObjectType == databaseObjectType && item.DifferenceType != DbDifferenceType.None)
            ? DbDifferenceType.Modified
            : DbDifferenceType.None;
    }

    private void DecorateData(List<DbDifference> differences)
    {
        var folderTypes = new Dictionary<DbObjectTreeFolderType, DbDifferenceType>();

        Action<DbObjectTreeFolderType, DbDifferenceType> addFolderType = (folderType, diffType) =>
        {
            if (!folderTypes.ContainsKey(folderType))
                folderTypes.Add(folderType, diffType);
            else if (diffType != DbDifferenceType.None) folderTypes[folderType] = DbDifferenceType.Modified;
        };

        foreach (var difference in differences)
        {
            var databaseObjectType = difference.DatabaseObjectType;

            switch (databaseObjectType)
            {
                case DatabaseObjectType.Type:
                    difference.ParentType = DbObjectTreeFolderType.Types.ToString();
                    addFolderType(DbObjectTreeFolderType.Types, difference.DifferenceType);
                    break;
                case DatabaseObjectType.Table:
                    difference.ParentType = DbObjectTreeFolderType.Tables.ToString();
                    addFolderType(DbObjectTreeFolderType.Tables, difference.DifferenceType);
                    break;
                case DatabaseObjectType.View:
                    difference.ParentType = DbObjectTreeFolderType.Views.ToString();
                    addFolderType(DbObjectTreeFolderType.Views, difference.DifferenceType);
                    break;
                case DatabaseObjectType.Function:
                    difference.ParentType = DbObjectTreeFolderType.Functions.ToString();
                    addFolderType(DbObjectTreeFolderType.Functions, difference.DifferenceType);
                    break;
                case DatabaseObjectType.Procedure:
                    difference.ParentType = DbObjectTreeFolderType.Procedures.ToString();
                    addFolderType(DbObjectTreeFolderType.Procedures, difference.DifferenceType);
                    break;
            }
        }

        var i = 0;
        foreach (var kp in folderTypes)
            differences.Insert(i++, new DbDifference { Type = kp.Key.ToString(), DifferenceType = kp.Value });
    }

    private async void btnSync_Click(object sender, EventArgs e)
    {
        await GenerateOrSync(true);
    }

    private async void btnGenerate_Click(object sender, EventArgs e)
    {
        await GenerateOrSync(false);
    }

    private async Task GenerateOrSync(bool isSync, DbDifference difference = null)
    {
        if (sourceInterpreter == null || targetInterpreter == null || differences == null)
        {
            MessageBox.Show("Please compare first.");
            return;
        }

        try
        {
            var dbSynchro = new DbSynchro(sourceInterpreter, targetInterpreter);

            if (!isSync)
            {
                List<Script> scripts = null;

                var targetDbSchema = GetTargetDbSchema();

                if (difference == null)
                    scripts = await dbSynchro.GenerateChangedScripts(sourceSchemaInfo, targetDbSchema, differences);
                else if (difference.Source is ScriptDbObject || difference.Target is ScriptDbObject)
                    scripts = dbSynchro.GenereateUserDefinedTypeChangedScripts(difference, targetDbSchema);
                else if (difference.DatabaseObjectType == DatabaseObjectType.Table)
                    scripts = await dbSynchro.GenerateTableChangedScripts(sourceSchemaInfo, difference, targetDbSchema);
                else if (difference.Source is TableChild || difference.Target is TableChild)
                    scripts = await dbSynchro.GenerateTableChildChangedScripts(difference);
                else if (difference.Source is UserDefinedType || difference.Target is UserDefinedType)
                    scripts = dbSynchro.GenereateUserDefinedTypeChangedScripts(difference, targetDbSchema);

                if (scripts != null)
                {
                    var strScripts = string.Join(Environment.NewLine, scripts.Select(item => item.Content));

                    var scriptsViewer = new frmScriptsViewer { DatabaseType = targetInterpreter.DatabaseType };
                    scriptsViewer.LoadScripts(StringHelper.ToSingleEmptyLine(strScripts).Trim());

                    scriptsViewer.ShowDialog();
                }
            }
            else
            {
                if (MessageBox.Show("Are you sure to sync changes to target database?", "Confirm",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    var result = await dbSynchro.Sync(sourceSchemaInfo, GetTargetDbSchema(), differences);

                    if (result.IsOK)
                        MessageBox.Show("sync successfully.");
                    else
                        MessageBox.Show(result.Message);
                }
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private void HandleException(Exception ex)
    {
        var errMsg = ExceptionHelper.GetExceptionDetails(ex);

        LogHelper.LogError(errMsg);

        Feedback(new FeedbackInfo { InfoType = FeedbackInfoType.Error, Message = errMsg });

        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private string GetTargetDbSchema()
    {
        if (targetInterpreter.DatabaseType == DatabaseType.Oracle)
            return (targetInterpreter as OracleInterpreter).GetDbSchema();
        if (targetInterpreter.DatabaseType == DatabaseType.MySql)
            return targetInterpreter.ConnectionInfo.Database;
        if (targetInterpreter.DatabaseType == DatabaseType.SqlServer) return "dbo";

        return null;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void sourceDbProfile_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        sourceDbConnectionInfo = connectionInfo;

        targetDbProfile.DatabaseType = sourceDbProfile.DatabaseType;
    }

    private void targetDbProfile_OnSelectedChanged(object sender, ConnectionInfo connectionInfo)
    {
        targetDbConnectionInfo = connectionInfo;
    }

    private void frmCompare_SizeChanged(object sender, EventArgs e)
    {
        splitContainer2.SplitterDistance = (int)((splitContainer2.Width - splitContainer2.SplitterWidth) * 1.0 / 2);
    }

    private void tlvDifferences_SelectedIndexChanged(object sender, EventArgs e)
    {
        var difference = tlvDifferences.SelectedObject as DbDifference;

        if (difference != null)
        {
            ShowScripts(difference);

            HighlightingDifferences(txtSource, txtTarget);
        }
    }

    private void ShowScripts(DbDifference difference)
    {
        var source = difference.Source;
        var target = difference.Target;

        ShowSourceScripts(GetDatabaseObjectScripts(source, true));
        ShowTargetScripts(GetDatabaseObjectScripts(target, false));
    }

    private string GetDatabaseObjectScripts(DatabaseObject dbObj, bool isSource)
    {
        if (dbObj == null) return string.Empty;

        var scriptGenerator = isSource ? sourceScriptGenerator : targetScriptGenerator;

        if (dbObj is Table table)
        {
            var schemaInfo = isSource ? sourceSchemaInfo : targetSchemaInfo;

            IEnumerable<TableColumn> columns = schemaInfo.TableColumns
                .Where(item => item.Schema == table.Schema && item.TableName == table.Name).OrderBy(item => item.Name);
            var tablePrimaryKey =
                schemaInfo.TablePrimaryKeys.FirstOrDefault(item =>
                    item.Schema == table.Schema && item.TableName == table.Name);
            var foreignKeys =
                schemaInfo.TableForeignKeys.Where(item => item.Schema == table.Schema && item.TableName == table.Name);
            IEnumerable<TableIndex> indexes = schemaInfo.TableIndexes
                .Where(item => item.Schema == table.Schema && item.TableName == table.Name).OrderBy(item => item.Name);
            var constraints =
                schemaInfo.TableConstraints.Where(item => item.Schema == table.Schema && item.TableName == table.Name);

            return scriptGenerator.CreateTable(table, columns, tablePrimaryKey, foreignKeys, indexes, constraints)
                .ToString();
        }

        return scriptGenerator.Create(dbObj).Content;
    }

    private void ShowSourceScripts(string scripts)
    {
        ShowScripts(txtSource, scripts);
    }

    private void ShowTargetScripts(string scripts)
    {
        ShowScripts(txtTarget, scripts);
    }

    private void ShowScripts(RichTextBox textBox, string scripts)
    {
        textBox.Clear();

        if (!string.IsNullOrEmpty(scripts))
        {
            textBox.AppendText(scripts.Trim());

            RichTextBoxHelper.Highlighting(textBox, targetDbProfile.DatabaseType, false);
        }
    }

    private void HighlightingDifferences(RichTextBox sourceTextBox, RichTextBox targetTextBox)
    {
        if (sourceTextBox.TextLength > 0 && targetTextBox.TextLength > 0)
        {
            var model = SideBySideDiffBuilder.Instance.BuildDiffModel(targetTextBox.Text, sourceTextBox.Text);

            HighlightingChanges(targetTextBox, model.OldText.Lines);
            HighlightingChanges(sourceTextBox, model.NewText.Lines);

            sourceTextBox.SelectionStart = 0;
            targetTextBox.SelectionStart = 0;
        }
    }

    private void HighlightingChanges(RichTextBox richTextBox, List<DiffPiece> lines)
    {
        var lineIndex = 0;

        foreach (var line in lines)
        {
            if (line.Position.HasValue && line.Type != ChangeType.Unchanged)
            {
                var lineFirstCharIndex = richTextBox.GetFirstCharIndexFromLine(lineIndex);

                if (line.Type == ChangeType.Inserted || line.Type == ChangeType.Deleted)
                {
                    HighlightingText(richTextBox, lineFirstCharIndex, line.Text.Length, line.Type);
                }
                else if (line.Type == ChangeType.Modified)
                {
                    var subLength = 0;

                    foreach (var subPiece in line.SubPieces)
                        if (subPiece.Text != null)
                        {
                            if (subPiece.Type != ChangeType.Unchanged)
                            {
                                var startIndex = lineFirstCharIndex + subLength;

                                HighlightingText(richTextBox, startIndex, subPiece.Text.Length, ChangeType.Modified);
                            }

                            subLength += subPiece.Text.Length;
                        }
                }
            }

            if (line.Type != ChangeType.Imaginary) lineIndex++;
        }
    }

    private void HighlightingText(RichTextBox richTextBox, int startIndex, int length, ChangeType changeType)
    {
        if (startIndex < 0) return;

        var color = Color.White;

        if (changeType == ChangeType.Modified)
            color = Color.Yellow;
        else if (changeType == ChangeType.Inserted)
            color = ColorTranslator.FromHtml("#B7EB9B");
        else if (changeType == ChangeType.Deleted) color = ColorTranslator.FromHtml("#F2C4C4");

        richTextBox.Select(startIndex, length);
        richTextBox.SelectionBackColor = color;
    }

    private void txtSource_VScroll(object sender, EventArgs e)
    {
        SyncScrollBar(sender as RichTextBox);
    }

    private void txtTarget_VScroll(object sender, EventArgs e)
    {
        SyncScrollBar(sender as RichTextBox);
    }

    private void SyncScrollBar(RichTextBox richTextBox)
    {
        if (isRichTextBoxScrolling) return;

        isRichTextBoxScrolling = true;

        SyncScrollBarLocation(richTextBox);

        isRichTextBoxScrolling = false;
    }

    private void SyncScrollBarLocation(RichTextBox richTextBox)
    {
        var point = richTextBox.Location;
        var charIndex = richTextBox.GetCharIndexFromPosition(point);
        var lineIndex = richTextBox.GetLineFromCharIndex(charIndex);

        var anotherTextbox = richTextBox.Name == txtSource.Name ? txtTarget : txtSource;

        var firstCharIndexOfLine = anotherTextbox.GetFirstCharIndexFromLine(lineIndex);

        if (firstCharIndexOfLine >= 0)
        {
            anotherTextbox.SelectionStart = firstCharIndexOfLine;
            anotherTextbox.SelectionLength = 0;
            anotherTextbox.ScrollToCaret();
        }
    }

    private void tsmiExpandAll_Click(object sender, EventArgs e)
    {
        var difference = tlvDifferences.SelectedObject as DbDifference;

        if (difference != null)
            ExpandCollapseAllChildren(difference, true);
        else
            tlvDifferences.ExpandAll();
    }

    private void ExpandCollapseAllChildren(DbDifference difference, bool isExpand)
    {
        if (tlvDifferences.CanExpand(difference))
        {
            if (isExpand)
                tlvDifferences.Expand(difference);
            else
                tlvDifferences.Collapse(difference);
        }

        var children = tlvDifferences.GetChildren(difference).OfType<DbDifference>();

        foreach (var child in children) ExpandCollapseAllChildren(child, isExpand);
    }

    private void tsmiCollapseAll_Click(object sender, EventArgs e)
    {
        var difference = tlvDifferences.SelectedObject as DbDifference;

        if (difference != null)
            ExpandCollapseAllChildren(difference, false);
        else
            tlvDifferences.CollapseAll();
    }

    private void Feedback(string message)
    {
        Feedback(new FeedbackInfo { Message = message });
    }

    private void Feedback(FeedbackInfo info)
    {
        Invoke(() =>
        {
            txtMessage.Text = info.Message;

            if (info.InfoType == FeedbackInfoType.Error)
            {
                txtMessage.ForeColor = Color.Red;
                toolTip1.SetToolTip(txtMessage, info.Message);
            }
            else
            {
                txtMessage.ForeColor = Color.Black;
            }
        });
    }

    private void tlvDifferences_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            if (tlvDifferences.Items.Count == 0) return;

            var difference = tlvDifferences.SelectedObject as DbDifference;

            if (difference != null)
            {
                tsmiCollapseAll.Visible = tsmiExpandAll.Visible = CanExpand(difference);

                tsmiGenerateChangedScripts.Visible = difference.DifferenceType != DbDifferenceType.None &&
                                                     difference.DatabaseObjectType != DatabaseObjectType.None;
            }
            else
            {
                tsmiCollapseAll.Visible = tsmiExpandAll.Visible = true;
            }

            contextMenuStrip1.Show(Cursor.Position);
        }
    }

    private async void tsmiGenerateChangedScripts_Click(object sender, EventArgs e)
    {
        var difference = tlvDifferences.SelectedObject as DbDifference;

        await GenerateOrSync(false, difference);
    }

    private void tlvDifferences_ItemsChanged(object sender, ItemsChangedEventArgs e)
    {
        if (tlvDifferences.Roots != null)
        {
            var roots = tlvDifferences.Roots.OfType<DbDifference>();
            var firstRecord = roots.FirstOrDefault();

            if (roots.Count() == 1 && CanExpand(firstRecord) && !tlvDifferences.IsExpanded(firstRecord))
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(1000);

                    try
                    {
                        tlvDifferences.Expand(firstRecord);
                    }
                    catch (Exception ex)
                    {
                    }
                });
        }
    }

    private void tsmiFindText_Click(object sender, EventArgs e)
    {
        FindText();
    }

    private void FindText()
    {
        var findBox = new frmFindBox();

        var result = findBox.ShowDialog();

        if (result == DialogResult.OK)
        {
            var word = findBox.FindWord;

            var objects = tlvDifferences.Objects;

            var found = false;

            if (objects != null) found = FindText(objects, word);

            if (!found) MessageBox.Show("Not found.");
        }
    }

    private bool FindText(IEnumerable objects, string word)
    {
        foreach (var obj in objects)
            if (obj is DbDifference diff)
            {
                if (diff.DatabaseObjectType == DatabaseObjectType.None)
                {
                    var children = tlvDifferences.GetChildren(diff);

                    return FindText(children, word);
                }

                if (IsNameMatch(diff.Source, word) || IsNameMatch(diff.Target, word))
                {
                    var index = tlvDifferences.TreeModel.GetObjectIndex(diff);

                    if (index >= 0)
                    {
                        tlvDifferences.SelectedIndex = index;

                        tlvDifferences.EnsureModelVisible(diff);

                        return true;
                    }
                }
            }

        return false;
    }

    private bool IsNameMatch(DatabaseObject dbObject, string word)
    {
        var name = dbObject?.Name;

        if (string.IsNullOrEmpty(name)) return false;

        var text = name.Split('.').LastOrDefault();

        if (text.ToUpper() == word.ToUpper()) return true;

        return false;
    }

    private void tlvDifferences_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control)
            if (e.KeyCode == Keys.F)
                FindText();
    }

    #region IObserver<FeedbackInfo>

    void IObserver<FeedbackInfo>.OnCompleted()
    {
    }

    void IObserver<FeedbackInfo>.OnError(Exception error)
    {
    }

    void IObserver<FeedbackInfo>.OnNext(FeedbackInfo info)
    {
        Feedback(info);
    }

    #endregion
}