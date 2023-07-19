using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Model;

namespace DatabaseManager.Controls;

public delegate void GeneateChangeScriptsHandler();

public delegate void ColumnSelectHandler(DatabaseObjectType databaseObjectType, IEnumerable<SimpleColumn> columns,
    bool columnIsReadOnly, bool isSingleSelect = false);

public partial class UC_TableDesigner : UserControl, IDbObjContentDisplayer, IObserver<FeedbackInfo>
{
    private readonly string selfTableName = "<self>";

    private DatabaseObjectDisplayInfo displayInfo;

    public FeedbackHandler OnFeedback;

    public UC_TableDesigner()
    {
        InitializeComponent();

        ucIndexes.OnColumnSelect += ShowColumnSelector;
        ucConstraints.OnColumnSelect += ShowColumnSelector;
        ucForeignKeys.OnColumnMappingSelect += ShowColumnMappingSelector;
    }

    public void Show(DatabaseObjectDisplayInfo displayInfo)
    {
        this.displayInfo = displayInfo;
        ucColumns.DatabaseType = displayInfo.DatabaseType;
        ucIndexes.DatabaseType = displayInfo.DatabaseType;
        ucForeignKeys.DatabaseType = displayInfo.DatabaseType;
        ucForeignKeys.DefaultSchema = GetDbInterpreter().DefaultSchema;
        ucConstraints.DatabaseType = displayInfo.DatabaseType;

        InitControls();
    }

    public ContentSaveResult Save(ContentSaveInfo info)
    {
        EndControlsEdit();

        var result = Task.Run(() => SaveTable()).Result;

        if (!result.IsOK)
        {
            MessageBox.Show(result.Message);
        }
        else
        {
            var message = "Table saved.";

            Feedback(message);

            MessageBox.Show(message);

            var table = result.ResultData as Table;

            displayInfo.DatabaseObject = table;
            ucColumns.OnSaved();
            ucIndexes.OnSaved();
            ucForeignKeys.OnSaved();
            ucConstraints.OnSaved();

            if (displayInfo.IsNew || table.Name != displayInfo.Name)
                if (FormEventCenter.OnRefreshNavigatorFolder != null)
                    FormEventCenter.OnRefreshNavigatorFolder();
        }

        return result;
    }

    private void UC_TableDesigner_Load(object sender, EventArgs e)
    {
        ucColumns.OnGenerateChangeScripts += GeneateChangeScripts;
        ucIndexes.OnGenerateChangeScripts += GeneateChangeScripts;
        ucForeignKeys.OnGenerateChangeScripts += GeneateChangeScripts;
        ucConstraints.OnGenerateChangeScripts += GeneateChangeScripts;
    }

    private async void InitControls()
    {
        if (displayInfo.DatabaseType == DatabaseType.Oracle)
        {
            lblSchema.Text = "Tablespace:";
        }
        else if (displayInfo.DatabaseType == DatabaseType.Sqlite)
        {
            lblSchema.Visible = false;
            cboSchema.Visible = false;
        }

        if (!ManagerUtil.SupportComment(displayInfo.DatabaseType))
            if (lblComment.Visible)
            {
                lblComment.Visible = txtTableComment.Visible = false;
                tabControl1.Top -= txtTableComment.Height + 10;
                tabControl1.Height += txtTableComment.Height + 10;
            }

        var dbInterpreter = GetDbInterpreter();

        var userDefinedTypes = await dbInterpreter.GetUserDefinedTypesAsync();

        ucColumns.UserDefinedTypes = userDefinedTypes;
        ucColumns.InitControls();

        if (displayInfo.IsNew)
        {
            if (displayInfo.DatabaseType != DatabaseType.Sqlite) LoadDatabaseSchemas();
        }
        else
        {
            cboSchema.Enabled = false;

            var filter = new SchemaInfoFilter
                { Strict = true, Schema = displayInfo.Schema, TableNames = new[] { displayInfo.Name } };
            filter.DatabaseObjectType =
                DatabaseObjectType.Table | DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey;

            var schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

            var table = schemaInfo.Tables.FirstOrDefault();

            if (table != null)
            {
                txtTableName.Text = table.Name;
                cboSchema.Text = table.Schema;
                txtTableComment.Text = table.Comment;

                #region Load Columns

                var columnDesingerInfos = ColumnManager.GetTableColumnDesingerInfos(dbInterpreter, table,
                    schemaInfo.TableColumns, schemaInfo.TablePrimaryKeys);

                ucColumns.LoadColumns(table, columnDesingerInfos);

                #endregion
            }
            else
            {
                MessageBox.Show("Table is not existed");
            }
        }
    }

    private async void LoadDatabaseSchemas()
    {
        var dbInterpreter = GetDbInterpreter();

        var items = new List<string>();
        string defaultItem = null;

        var schemas = await dbInterpreter.GetDatabaseSchemasAsync();

        items.AddRange(schemas.Select(item => item.Name));

        var defaultSchema = dbInterpreter.DefaultSchema;

        if (!string.IsNullOrEmpty(defaultSchema) && schemas.Any(item => item.Name == defaultSchema))
            defaultItem = defaultSchema;

        if (displayInfo.DatabaseType == DatabaseType.Oracle || displayInfo.DatabaseType == DatabaseType.MySql)
            cboSchema.Enabled = false;

        cboSchema.Items.AddRange(items.ToArray());

        if (cboSchema.Items.Count == 1)
        {
            cboSchema.SelectedIndex = 0;
        }
        else
        {
            if (defaultItem != null) cboSchema.Text = defaultItem;
        }
    }

    private DbInterpreter GetDbInterpreter()
    {
        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(displayInfo.DatabaseType, displayInfo.ConnectionInfo,
            new DbInterpreterOption());

        return dbInterpreter;
    }

    private SchemaDesignerInfo GetSchemaDesingerInfo()
    {
        var schemaDesingerInfo = new SchemaDesignerInfo();

        var tableDesignerInfo = new TableDesignerInfo
        {
            Name = txtTableName.Text.Trim(),
            Schema = cboSchema.Text.Trim(),
            Comment = txtTableComment.Text.Trim(),
            OldName = displayInfo.DatabaseObject?.Name
        };

        schemaDesingerInfo.TableDesignerInfo = tableDesignerInfo;

        var columns = ucColumns.GetColumns();

        columns.ForEach(item =>
        {
            item.Schema = tableDesignerInfo.Schema;
            item.TableName = tableDesignerInfo.Name;
        });

        schemaDesingerInfo.TableColumnDesingerInfos.AddRange(columns);

        if (tabIndexes.Tag == null)
        {
            schemaDesingerInfo.IgnoreTableIndex = true;
        }
        else
        {
            var indexes = ucIndexes.GetIndexes();

            indexes.ForEach(item =>
            {
                item.Schema = tableDesignerInfo.Schema;
                item.TableName = tableDesignerInfo.Name;
            });

            schemaDesingerInfo.TableIndexDesingerInfos.AddRange(indexes);
        }

        if (tabForeignKeys.Tag == null)
        {
            schemaDesingerInfo.IgnoreTableForeignKey = true;
        }
        else
        {
            var foreignKeys = ucForeignKeys.GetForeignKeys();

            foreignKeys.ForEach(item =>
            {
                item.Schema = tableDesignerInfo.Schema;
                item.TableName = tableDesignerInfo.Name;

                if (item.ReferencedTableName == selfTableName)
                {
                    item.ReferencedTableName = tableDesignerInfo.Name;
                    item.Name = item.Name.Replace(selfTableName, tableDesignerInfo.Name);
                }
            });

            schemaDesingerInfo.TableForeignKeyDesignerInfos.AddRange(foreignKeys);
        }

        if (tabConstraints.Tag == null)
        {
            schemaDesingerInfo.IgnoreTableConstraint = true;
        }
        else
        {
            var constraints = ucConstraints.GetConstraints();

            constraints.ForEach(item =>
            {
                item.Schema = tableDesignerInfo.Schema;
                item.TableName = tableDesignerInfo.Name;
            });

            schemaDesingerInfo.TableConstraintDesignerInfos.AddRange(constraints);
        }

        return schemaDesingerInfo;
    }

    private TableManager GetTableManager()
    {
        var dbInterpreter = GetDbInterpreter();

        var tableManager = new TableManager(dbInterpreter);

        return tableManager;
    }

    private async Task<ContentSaveResult> SaveTable()
    {
        var schemaDesignerInfo = GetSchemaDesingerInfo();

        var tableManager = GetTableManager();

        Feedback("Saving table...");

        return await tableManager.Save(schemaDesignerInfo, displayInfo.IsNew);
    }

    private void EndControlsEdit()
    {
        ucColumns.EndEdit();
        ucIndexes.EndEdit();
        ucForeignKeys.EndEdit();
        ucConstraints.EndEdit();
    }

    internal async Task<bool> IsChanged()
    {
        var result = await GetChangedScripts();

        if (result.IsOK)
        {
            var scriptsData = result.ResultData as TableDesignerGenerateScriptsData;

            if (scriptsData.Scripts.Count > 0 &&
                !scriptsData.Scripts.All(item => string.IsNullOrEmpty(item.Content))) return true;
        }

        return false;
    }

    private async Task<ContentSaveResult> GetChangedScripts()
    {
        EndControlsEdit();

        var schemaDesignerInfo = GetSchemaDesingerInfo();

        var tableManager = GetTableManager();

        Feedback("Generating changed scripts...");

        var result = await tableManager.GenerateChangeScripts(schemaDesignerInfo, displayInfo.IsNew);

        Feedback("End generate changed scripts.");

        return result;
    }

    private async void GeneateChangeScripts()
    {
        var result = await GetChangedScripts();

        if (!result.IsOK)
        {
            MessageBox.Show(result.Message);
        }
        else
        {
            var scriptsData = result.ResultData as TableDesignerGenerateScriptsData;

            var scripts = string.Join(Environment.NewLine, scriptsData.Scripts.Select(item => item.Content));

            var scriptsViewer = new frmScriptsViewer { DatabaseType = displayInfo.DatabaseType };
            scriptsViewer.LoadScripts(StringHelper.ToSingleEmptyLine(scripts).Trim());

            scriptsViewer.ShowDialog();
        }
    }

    private async void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
    {
        var tabPage = tabControl1.SelectedTab;

        if (tabPage == null) return;

        var table = new Table { Schema = cboSchema.Text, Name = txtTableName.Text.Trim() };

        var dbInterpreter = GetDbInterpreter();

        if (tabPage.Name == tabIndexes.Name)
        {
            tabPage.Tag = 1;

            ucIndexes.Table = table;

            if (!ucIndexes.Inited) ucIndexes.InitControls(dbInterpreter);

            if (!displayInfo.IsNew)
                if (!ucIndexes.LoadedData)
                {
                    var filter = new SchemaInfoFilter();
                    filter.Schema = displayInfo.Schema;
                    filter.TableNames = new[] { displayInfo.Name };

                    var tableIndexes = await dbInterpreter.GetTableIndexesAsync(filter, true);

                    ucIndexes.LoadIndexes(IndexManager.GetIndexDesignerInfos(displayInfo.DatabaseType, tableIndexes));
                }

            var columns = ucColumns.GetColumns().Where(item => !string.IsNullOrEmpty(item.Name) && item.IsPrimary);

            ucIndexes.LoadPrimaryKeys(columns);
        }
        else if (tabPage.Name == tabForeignKeys.Name)
        {
            tabPage.Tag = 1;

            ucForeignKeys.Table = table;

            if (!ucForeignKeys.Inited)
            {
                dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

                var tables = await dbInterpreter.GetTablesAsync();

                if (displayInfo.IsNew) tables.Add(new Table { Name = "<self>" });

                ucForeignKeys.InitControls(tables);
            }

            if (!displayInfo.IsNew)
                if (!ucForeignKeys.LoadedData)
                {
                    var filter = new SchemaInfoFilter();

                    filter.Schema = displayInfo.Schema;
                    filter.TableNames = new[] { displayInfo.Name };

                    dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Details;

                    var foreignKeys = await dbInterpreter.GetTableForeignKeysAsync(filter);

                    ucForeignKeys.LoadForeignKeys(IndexManager.GetForeignKeyDesignerInfos(foreignKeys));
                }
        }
        else if (tabPage.Name == tabConstraints.Name)
        {
            tabPage.Tag = 1;

            if (!ucConstraints.Inited) ucConstraints.InitControls();

            if (!displayInfo.IsNew)
                if (!ucConstraints.LoadedData)
                {
                    var filter = new SchemaInfoFilter();

                    filter.Schema = displayInfo.Schema;
                    filter.TableNames = new[] { displayInfo.Name };

                    var constraints = await dbInterpreter.GetTableConstraintsAsync(filter);

                    ucConstraints.LoadConstraints(IndexManager.GetConstraintDesignerInfos(constraints));
                }
        }
    }

    private void ShowColumnSelector(DatabaseObjectType databaseObjectType, IEnumerable<SimpleColumn> values,
        bool columnIsReadonly, bool isSingleSelect)
    {
        var columnSelect = new frmColumSelect { ColumnIsReadOnly = columnIsReadonly, IsSingleSelect = isSingleSelect };

        var columns = ucColumns.GetColumns().Where(item => !string.IsNullOrEmpty(item.Name));

        var columnInfos = new List<SimpleColumn>();

        foreach (var column in columns)
            if (databaseObjectType == DatabaseObjectType.Index)
            {
                if (!string.IsNullOrEmpty(column.DataType) &&
                    string.IsNullOrEmpty(column.ExtraPropertyInfo?.Expression))
                {
                    var dataTypeSpec =
                        DataTypeManager.GetDataTypeSpecification(displayInfo.DatabaseType, column.DataType);

                    if (dataTypeSpec != null && !dataTypeSpec.IndexForbidden)
                        columnInfos.Add(new IndexColumn { ColumnName = column.Name });
                }
            }
            else
            {
                columnInfos.Add(new SimpleColumn { ColumnName = column.Name });
            }

        columnSelect.InitControls(columnInfos, displayInfo.DatabaseType == DatabaseType.SqlServer);

        if (databaseObjectType == DatabaseObjectType.Index)
            columnSelect.LoadColumns(values.Select(item => item as IndexColumn));
        else
            columnSelect.LoadColumns(values);

        if (columnSelect.ShowDialog() == DialogResult.OK)
        {
            if (databaseObjectType == DatabaseObjectType.Index)
                ucIndexes.SetRowColumns(columnSelect.SelectedColumns.Select(item => item as IndexColumn));
            else if (databaseObjectType == DatabaseObjectType.Constraint)
                ucConstraints.SetRowColumns(columnSelect.SelectedColumns);
        }
    }

    private async void ShowColumnMappingSelector(string referenceSchema, string referenceTableName,
        List<ForeignKeyColumn> mappings)
    {
        var form = new frmColumnMapping
            { ReferenceTableName = referenceTableName, TableName = txtTableName.Text.Trim(), Mappings = mappings };

        var columns = ucColumns.GetColumns().Where(item => !string.IsNullOrEmpty(item.Name));

        form.TableColumns = columns.OrderBy(item => item.Name).Select(item => item.Name).ToList();

        var dbInterpreter = GetDbInterpreter();
        dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

        var filter = new SchemaInfoFilter { Schema = referenceSchema, TableNames = new[] { referenceTableName } };
        var referenceTableColumns = await dbInterpreter.GetTableColumnsAsync(filter);

        if (referenceTableName == selfTableName)
            form.ReferenceTableColumns = ucColumns.GetColumns().Select(item => item.Name).ToList();
        else
            form.ReferenceTableColumns = referenceTableColumns.Select(item => item.Name).ToList();

        if (form.ShowDialog() == DialogResult.OK) ucForeignKeys.SetRowColumns(form.Mappings);
    }

    private void Feedback(string message)
    {
        Feedback(new FeedbackInfo { InfoType = FeedbackInfoType.Info, Message = message, Owner = this });
    }

    private void FeedbackError(string message)
    {
        Feedback(new FeedbackInfo { InfoType = FeedbackInfoType.Error, Message = message, Owner = this });
    }

    private void Feedback(FeedbackInfo info)
    {
        OnFeedback?.Invoke(info);
    }

    #region IObserver<FeedbackInfo>

    public void OnNext(FeedbackInfo value)
    {
        Feedback(value);
    }

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }

    #endregion
}