using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class TableManager
    {
        private readonly DbInterpreter dbInterpreter;
        private IObserver<FeedbackInfo> observer;
        private readonly DbScriptGenerator scriptGenerator;

        public TableManager(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;

            scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(this.dbInterpreter);
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public async Task<ContentSaveResult> Save(SchemaDesignerInfo schemaDesignerInfo, bool isNew)
        {
            Table table = null;

            try
            {
                var result = await GenerateChangeScripts(schemaDesignerInfo, isNew);

                if (!result.IsOK) return result;

                var scriptsData = result.ResultData as TableDesignerGenerateScriptsData;

                var scripts = scriptsData.Scripts;

                table = scriptsData.Table;

                if (scripts == null || scripts.Count == 0) return GetFaultSaveResult("No changes need to save.");

                var scriptRunner = new ScriptRunner();

                await scriptRunner.Run(dbInterpreter, scripts);
            }
            catch (Exception ex)
            {
                var errMsg = ExceptionHelper.GetExceptionDetails(ex);

                FeedbackError(errMsg);

                return GetFaultSaveResult(errMsg);
            }

            return new ContentSaveResult { IsOK = true, ResultData = table };
        }

        public async Task<ContentSaveResult> GenerateChangeScripts(SchemaDesignerInfo schemaDesignerInfo, bool isNew)
        {
            string validateMsg;

            Table table = null;

            try
            {
                var isValid = ValidateModel(schemaDesignerInfo, out validateMsg);

                if (!isValid) return GetFaultSaveResult(validateMsg);

                var scriptsData = new TableDesignerGenerateScriptsData();

                var schemaInfo = GetSchemaInfo(schemaDesignerInfo);

                table = schemaInfo.Tables.First();

                scriptsData.Table = table;

                var scripts = new List<Script>();

                if (isNew)
                {
                    var scriptBuilder = scriptGenerator.GenerateSchemaScripts(schemaInfo);

                    scripts.AddRange(scriptBuilder.Scripts);
                }
                else
                {
                    #region Alter Table

                    var tableDesignerInfo = schemaDesignerInfo.TableDesignerInfo;

                    var filter = new SchemaInfoFilter { Strict = true };
                    filter.Schema = tableDesignerInfo.Schema;
                    filter.TableNames = new[] { tableDesignerInfo.OldName };
                    filter.DatabaseObjectType = DatabaseObjectType.Table
                                                | DatabaseObjectType.Column
                                                | DatabaseObjectType.PrimaryKey
                                                | DatabaseObjectType.ForeignKey
                                                | DatabaseObjectType.Index
                                                | DatabaseObjectType.Constraint;

                    dbInterpreter.Option.IncludePrimaryKeyWhenGetTableIndex = true;

                    var oldSchemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);
                    var oldTable = oldSchemaInfo.Tables.FirstOrDefault();

                    if (oldTable == null)
                        return GetFaultSaveResult($"Table \"{tableDesignerInfo.OldName}\" is not existed");

                    if (tableDesignerInfo.OldName != tableDesignerInfo.Name)
                        scripts.Add(scriptGenerator.RenameTable(
                            new Table { Schema = tableDesignerInfo.Schema, Name = tableDesignerInfo.OldName },
                            tableDesignerInfo.Name));

                    if (!IsStringEquals(tableDesignerInfo.Comment, oldTable.Comment))
                    {
                        oldTable.Comment = tableDesignerInfo.Comment;
                        scripts.Add(scriptGenerator.SetTableComment(oldTable, string.IsNullOrEmpty(oldTable.Comment)));
                    }

                    #region Columns

                    var columnDesingerInfos = schemaDesignerInfo.TableColumnDesingerInfos;
                    var oldColumns = oldSchemaInfo.TableColumns;

                    var defaultValueConstraints = await GetTableDefaultConstraints(filter);

                    var renamedColNames = new List<string>();

                    foreach (var columnDesignerInfo in columnDesingerInfos)
                    {
                        var oldName = columnDesignerInfo.OldName;

                        var oldColumn = oldColumns.FirstOrDefault(item => item.Name == oldName);
                        var newColumn =
                            schemaInfo.TableColumns.FirstOrDefault(item => item.Name == columnDesignerInfo.Name);

                        if (oldColumn == null)
                        {
                            scripts.Add(scriptGenerator.AddTableColumn(table, newColumn));
                        }
                        else
                        {
                            if (IsNameChanged(columnDesignerInfo.OldName, columnDesignerInfo.Name))
                            {
                                scripts.Add(GetColumnRenameScript(table, oldColumn, newColumn));

                                renamedColNames.Add(oldColumn.Name);
                            }

                            scripts.AddRange(GetColumnAlterScripts(oldTable, table, oldColumn, newColumn,
                                defaultValueConstraints));
                        }
                    }

                    foreach (var oldColumn in oldColumns)
                        if (!renamedColNames.Contains(oldColumn.Name) &&
                            !columnDesingerInfos.Any(item => item.Name == oldColumn.Name))
                            scripts.Add(scriptGenerator.DropTableColumn(oldColumn));

                    #endregion

                    #region Primary Key

                    var oldPrimaryKey = oldSchemaInfo.TablePrimaryKeys.FirstOrDefault();
                    var newPrimaryKey = schemaInfo.TablePrimaryKeys.FirstOrDefault();

                    scripts.AddRange(GetPrimaryKeyAlterScripts(oldPrimaryKey, newPrimaryKey,
                        schemaDesignerInfo.IgnoreTableIndex));

                    #endregion

                    #region Index

                    if (!schemaDesignerInfo.IgnoreTableIndex)
                    {
                        var oldIndexes = oldSchemaInfo.TableIndexes.Where(item => !item.IsPrimary);

                        var indexDesignerInfos =
                            schemaDesignerInfo.TableIndexDesingerInfos.Where(item => !item.IsPrimary);

                        foreach (var indexDesignerInfo in indexDesignerInfos)
                        {
                            var newIndex =
                                schemaInfo.TableIndexes.FirstOrDefault(item => item.Name == indexDesignerInfo.Name);

                            var oldIndex = oldIndexes.FirstOrDefault(item => item.Name == indexDesignerInfo.OldName);

                            if (IsValueEqualsIgnoreCase(indexDesignerInfo.OldName, indexDesignerInfo.Name)
                                && (IsValueEqualsIgnoreCase(indexDesignerInfo.OldType, indexDesignerInfo.Type) ||
                                    (indexDesignerInfo.Type == nameof(IndexType.Unique) && oldIndex.IsUnique))
                               )
                                if (oldIndex != null && IsStringEquals(oldIndex.Comment, newIndex.Comment) &&
                                    SchemaInfoHelper.IsIndexColumnsEquals(oldIndex.Columns, newIndex.Columns))
                                    continue;

                            scripts.AddRange(GetIndexAlterScripts(oldIndex, newIndex));
                        }

                        foreach (var oldIndex in oldIndexes)
                            if (!indexDesignerInfos.Any(item => item.Name == oldIndex.Name))
                                scripts.Add(scriptGenerator.DropIndex(oldIndex));
                    }

                    #endregion

                    #region Foreign Key

                    if (!schemaDesignerInfo.IgnoreTableForeignKey)
                    {
                        var oldForeignKeys = oldSchemaInfo.TableForeignKeys;

                        IEnumerable<TableForeignKeyDesignerInfo> foreignKeyDesignerInfos =
                            schemaDesignerInfo.TableForeignKeyDesignerInfos;

                        foreach (var foreignKeyDesignerInfo in foreignKeyDesignerInfos)
                        {
                            var newForeignKey =
                                schemaInfo.TableForeignKeys.FirstOrDefault(item =>
                                    item.Name == foreignKeyDesignerInfo.Name);

                            var oldForeignKey =
                                oldForeignKeys.FirstOrDefault(item => item.Name == foreignKeyDesignerInfo.OldName);

                            if (IsValueEqualsIgnoreCase(foreignKeyDesignerInfo.OldName, foreignKeyDesignerInfo.Name) &&
                                foreignKeyDesignerInfo.UpdateCascade == oldForeignKey.UpdateCascade &&
                                foreignKeyDesignerInfo.DeleteCascade == oldForeignKey.DeleteCascade)
                                if (oldForeignKey != null && IsStringEquals(oldForeignKey.Comment,
                                                              newForeignKey.Comment)
                                                          && oldForeignKey.ReferencedSchema ==
                                                          newForeignKey.ReferencedSchema &&
                                                          oldForeignKey.ReferencedTableName ==
                                                          newForeignKey.ReferencedTableName
                                                          && SchemaInfoHelper.IsForeignKeyColumnsEquals(
                                                              oldForeignKey.Columns, newForeignKey.Columns))
                                    continue;

                            scripts.AddRange(GetForeignKeyAlterScripts(oldForeignKey, newForeignKey));
                        }

                        foreach (var oldForeignKey in oldForeignKeys)
                            if (!foreignKeyDesignerInfos.Any(item => item.Name == oldForeignKey.Name))
                                scripts.Add(scriptGenerator.DropForeignKey(oldForeignKey));
                    }

                    #endregion

                    #region Constraint

                    if (!schemaDesignerInfo.IgnoreTableConstraint)
                    {
                        var oldConstraints = oldSchemaInfo.TableConstraints;

                        IEnumerable<TableConstraintDesignerInfo> constraintDesignerInfos =
                            schemaDesignerInfo.TableConstraintDesignerInfos;

                        foreach (var constraintDesignerInfo in constraintDesignerInfos)
                        {
                            var newConstraint =
                                schemaInfo.TableConstraints.FirstOrDefault(item =>
                                    item.Name == constraintDesignerInfo.Name);

                            var oldConstraint =
                                oldConstraints.FirstOrDefault(item => item.Name == constraintDesignerInfo.OldName);

                            if (IsValueEqualsIgnoreCase(constraintDesignerInfo.OldName, constraintDesignerInfo.Name))
                                if (oldConstraint != null && IsStringEquals(oldConstraint.Comment,
                                                              newConstraint.Comment)
                                                          && IsStringEquals(oldConstraint.Definition,
                                                              newConstraint.Definition))
                                    continue;

                            scripts.AddRange(GetConstraintAlterScripts(oldConstraint, newConstraint));
                        }

                        foreach (var oldConstraint in oldConstraints)
                            if (!constraintDesignerInfos.Any(item => item.Name == oldConstraint.Name))
                                scripts.Add(scriptGenerator.DropCheckConstraint(oldConstraint));
                    }

                    #endregion

                    #endregion
                }

                scriptsData.Scripts.AddRange(scripts);

                return new ContentSaveResult { IsOK = true, ResultData = scriptsData };
            }
            catch (Exception ex)
            {
                return GetFaultSaveResult(ExceptionHelper.GetExceptionDetails(ex));
            }
        }

        public Script GetColumnRenameScript(Table table, TableColumn oldColumn, TableColumn newColumn)
        {
            return scriptGenerator.RenameTableColumn(table, oldColumn, newColumn.Name);
        }

        public async Task<List<TableDefaultValueConstraint>> GetTableDefaultConstraints(SchemaInfoFilter filter)
        {
            List<TableDefaultValueConstraint> defaultValueConstraints = null;

            if (dbInterpreter.DatabaseType == DatabaseType.SqlServer)
                defaultValueConstraints =
                    await (dbInterpreter as SqlServerInterpreter).GetTableDefautValueConstraintsAsync(filter);

            return defaultValueConstraints;
        }

        public List<Script> GetColumnAlterScripts(Table oldTable, Table newTable, TableColumn oldColumn,
            TableColumn newColumn, List<TableDefaultValueConstraint> defaultValueConstraints)
        {
            var scripts = new List<Script>();

            var databaseType = dbInterpreter.DatabaseType;

            var isDefaultValueEquals = ValueHelper.IsStringEquals(
                StringHelper.GetBalanceParenthesisTrimedValue(oldColumn.DefaultValue), newColumn.DefaultValue);

            if (!SchemaInfoHelper.IsTableColumnEquals(databaseType, oldColumn, newColumn)
                || !isDefaultValueEquals)
            {
                if (!isDefaultValueEquals)
                    if (databaseType == DatabaseType.SqlServer)
                    {
                        var sqlServerScriptGenerator = scriptGenerator as SqlServerScriptGenerator;

                        var defaultValueConstraint = defaultValueConstraints?.FirstOrDefault(item =>
                            item.Schema == oldTable.Schema && item.TableName == oldTable.Name &&
                            item.ColumnName == oldColumn.Name);

                        if (defaultValueConstraint != null)
                            scripts.Add(sqlServerScriptGenerator.DropDefaultValueConstraint(defaultValueConstraint));

                        if (newColumn.DefaultValue != null)
                            scripts.Add(sqlServerScriptGenerator.AddDefaultValueConstraint(newColumn));
                    }

                var oldColumnDefinition = dbInterpreter.ParseColumn(newTable, oldColumn);
                var newColumnDefinition = dbInterpreter.ParseColumn(newTable, newColumn);

                if (!IsDefinitionEquals(oldColumnDefinition, newColumnDefinition))
                {
                    var alterColumnScript = scriptGenerator.AlterTableColumn(newTable, newColumn, oldColumn);

                    if (databaseType == DatabaseType.Oracle)
                    {
                        if (!oldColumn.IsNullable && !newColumn.IsNullable)
                            alterColumnScript.Content = Regex.Replace(alterColumnScript.Content, "NOT NULL", "",
                                RegexOptions.IgnoreCase);
                        else if (oldColumn.IsNullable && newColumn.IsNullable)
                            alterColumnScript.Content = Regex.Replace(alterColumnScript.Content, "NULL", "",
                                RegexOptions.IgnoreCase);
                    }

                    scripts.Add(alterColumnScript);
                }
            }
            else if (!ValueHelper.IsStringEquals(newColumn.Comment, oldColumn.Comment))
            {
                scripts.Add(scriptGenerator.SetTableColumnComment(newTable, newColumn,
                    string.IsNullOrEmpty(oldColumn.Comment)));
            }

            return scripts;
        }

        private bool IsDefinitionEquals(string definiton1, string defintion2)
        {
            return GetNoWhiteSpaceTrimedString(definiton1.ToUpper()) ==
                   GetNoWhiteSpaceTrimedString(defintion2.ToUpper());
        }

        private string GetNoWhiteSpaceTrimedString(string value)
        {
            return value.Replace(" ", "").Trim();
        }

        public List<Script> GetPrimaryKeyAlterScripts(TablePrimaryKey oldPrimaryKey, TablePrimaryKey newPrimaryKey,
            bool onlyCompareColumns)
        {
            var scripts = new List<Script>();

            var primaryKeyChanged =
                !SchemaInfoHelper.IsPrimaryKeyEquals(oldPrimaryKey, newPrimaryKey, onlyCompareColumns);

            Action alterPrimaryKey = () =>
            {
                if (oldPrimaryKey != null) scripts.Add(scriptGenerator.DropPrimaryKey(oldPrimaryKey));

                if (newPrimaryKey != null)
                {
                    scripts.Add(scriptGenerator.AddPrimaryKey(newPrimaryKey));

                    if (!string.IsNullOrEmpty(newPrimaryKey.Comment))
                        SetTableChildComment(scripts, scriptGenerator, newPrimaryKey, true);
                }
            };

            if (primaryKeyChanged)
            {
                alterPrimaryKey();
            }
            else if (!ValueHelper.IsStringEquals(oldPrimaryKey?.Comment, newPrimaryKey?.Comment))
            {
                if (dbInterpreter.DatabaseType == DatabaseType.SqlServer)
                    SetTableChildComment(scripts, scriptGenerator, newPrimaryKey,
                        string.IsNullOrEmpty(oldPrimaryKey?.Comment));
                else
                    alterPrimaryKey();
            }

            return scripts;
        }

        public List<Script> GetForeignKeyAlterScripts(TableForeignKey oldForeignKey, TableForeignKey newForeignKey)
        {
            var scripts = new List<Script>();

            if (oldForeignKey != null) scripts.Add(scriptGenerator.DropForeignKey(oldForeignKey));

            scripts.Add(scriptGenerator.AddForeignKey(newForeignKey));

            if (!string.IsNullOrEmpty(newForeignKey.Comment))
                SetTableChildComment(scripts, scriptGenerator, newForeignKey, true);

            return scripts;
        }

        public List<Script> GetIndexAlterScripts(TableIndex oldIndex, TableIndex newIndex)
        {
            var scripts = new List<Script>();

            if (oldIndex != null) scripts.Add(scriptGenerator.DropIndex(oldIndex));

            scripts.Add(scriptGenerator.AddIndex(newIndex));

            if (!string.IsNullOrEmpty(newIndex.Comment)) SetTableChildComment(scripts, scriptGenerator, newIndex, true);

            return scripts;
        }

        public List<Script> GetConstraintAlterScripts(TableConstraint oldConstraint, TableConstraint newConstraint)
        {
            var scripts = new List<Script>();

            if (oldConstraint != null) scripts.Add(scriptGenerator.DropCheckConstraint(oldConstraint));

            scripts.Add(scriptGenerator.AddCheckConstraint(newConstraint));

            if (!string.IsNullOrEmpty(newConstraint.Comment))
                SetTableChildComment(scripts, scriptGenerator, newConstraint, true);

            return scripts;
        }

        public bool IsValueEqualsIgnoreCase(string value1, string value2)
        {
            return !string.IsNullOrEmpty(value1) && !string.IsNullOrEmpty(value2) &&
                   value1.ToLower() == value2.ToLower();
        }

        public bool IsNameChanged(string name1, string name2)
        {
            if (SettingManager.Setting.DbObjectNameMode == DbObjectNameMode.WithoutQuotation)
            {
                if (IsValueEqualsIgnoreCase(name1, name2))
                    return false;
                return true;
            }

            var databaseType = dbInterpreter.DatabaseType;

            if (name1 == name2)
                return false;
            return true;
        }

        private void SetTableChildComment(List<Script> scripts, DbScriptGenerator scriptGenerator,
            TableChild tableChild, bool isNew)
        {
            if (dbInterpreter.DatabaseType == DatabaseType.SqlServer)
                scripts.Add((scriptGenerator as SqlServerScriptGenerator).SetTableChildComment(tableChild, isNew));
        }

        private bool IsStringEquals(string str1, string str2)
        {
            return ValueHelper.IsStringEquals(str1, str2);
        }

        private ContentSaveResult GetFaultSaveResult(string message)
        {
            return new ContentSaveResult { ResultData = message };
        }

        public SchemaInfo GetSchemaInfo(SchemaDesignerInfo schemaDesignerInfo)
        {
            var schemaInfo = new SchemaInfo();

            var table = new Table();
            ObjectHelper.CopyProperties(schemaDesignerInfo.TableDesignerInfo, table);

            schemaInfo.Tables.Add(table);

            #region Columns

            TablePrimaryKey primaryKey = null;

            foreach (var column in schemaDesignerInfo.TableColumnDesingerInfos)
            {
                var tableColumn = new TableColumn();
                ObjectHelper.CopyProperties(column, tableColumn);

                if (!DataTypeHelper.IsUserDefinedType(tableColumn))
                    ColumnManager.SetColumnLength(dbInterpreter.DatabaseType, tableColumn, column.Length);

                if (column.IsPrimary)
                {
                    if (primaryKey == null)
                        primaryKey = new TablePrimaryKey
                        {
                            Schema = table.Schema, TableName = table.Name,
                            Name = IndexManager.GetPrimaryKeyDefaultName(table)
                        };

                    var indexColumn = new IndexColumn
                        { ColumnName = column.Name, IsDesc = false, Order = primaryKey.Columns.Count + 1 };

                    if (!schemaDesignerInfo.IgnoreTableIndex)
                    {
                        var indexDesignerInfo = schemaDesignerInfo.TableIndexDesingerInfos
                            .FirstOrDefault(item =>
                                item.Type == IndexType.Primary.ToString() &&
                                item.Columns.Any(t => t.ColumnName == column.Name));

                        if (indexDesignerInfo != null)
                        {
                            primaryKey.Name = indexDesignerInfo.Name;
                            primaryKey.Comment = indexDesignerInfo.Comment;

                            var columnInfo =
                                indexDesignerInfo.Columns.FirstOrDefault(item => item.ColumnName == column.Name);

                            if (columnInfo != null) indexColumn.IsDesc = columnInfo.IsDesc;

                            if (indexDesignerInfo.ExtraPropertyInfo != null)
                                primaryKey.Clustered = indexDesignerInfo.ExtraPropertyInfo.Clustered;
                        }
                    }

                    primaryKey.Columns.Add(indexColumn);
                }

                var extralProperty = column.ExtraPropertyInfo;

                if (column.IsIdentity)
                {
                    if (extralProperty != null)
                    {
                        table.IdentitySeed = extralProperty.Seed;
                        table.IdentityIncrement = extralProperty.Increment;
                    }
                    else
                    {
                        table.IdentitySeed = 1;
                        table.IdentityIncrement = 1;
                    }
                }

                if (extralProperty?.Expression != null) tableColumn.ComputeExp = extralProperty.Expression;

                schemaInfo.TableColumns.Add(tableColumn);
            }

            if (primaryKey != null) schemaInfo.TablePrimaryKeys.Add(primaryKey);

            #endregion

            #region Indexes

            if (!schemaDesignerInfo.IgnoreTableIndex)
                foreach (var indexDesignerInfo in schemaDesignerInfo.TableIndexDesingerInfos)
                    if (!indexDesignerInfo.IsPrimary)
                    {
                        var index = new TableIndex
                            { Schema = indexDesignerInfo.Schema, TableName = indexDesignerInfo.TableName };
                        index.Name = indexDesignerInfo.Name;

                        index.IsUnique = indexDesignerInfo.Type == IndexType.Unique.ToString();
                        index.Clustered = indexDesignerInfo.Clustered;
                        index.Comment = indexDesignerInfo.Comment;
                        index.Type = indexDesignerInfo.Type;

                        index.Columns.AddRange(indexDesignerInfo.Columns);

                        var order = 1;
                        index.Columns.ForEach(item => { item.Order = order++; });

                        schemaInfo.TableIndexes.Add(index);
                    }

            #endregion

            #region Foreign Keys

            if (!schemaDesignerInfo.IgnoreTableForeignKey)
                foreach (var keyDesignerInfo in schemaDesignerInfo.TableForeignKeyDesignerInfos)
                {
                    var foreignKey = new TableForeignKey
                        { Schema = keyDesignerInfo.Schema, TableName = keyDesignerInfo.TableName };
                    foreignKey.Name = keyDesignerInfo.Name;

                    foreignKey.ReferencedSchema = keyDesignerInfo.ReferencedSchema;
                    foreignKey.ReferencedTableName = keyDesignerInfo.ReferencedTableName;
                    foreignKey.UpdateCascade = keyDesignerInfo.UpdateCascade;
                    foreignKey.DeleteCascade = keyDesignerInfo.DeleteCascade;
                    foreignKey.Comment = keyDesignerInfo.Comment;

                    foreignKey.Columns.AddRange(keyDesignerInfo.Columns);

                    var order = 1;
                    foreignKey.Columns.ForEach(item => { item.Order = order++; });

                    schemaInfo.TableForeignKeys.Add(foreignKey);
                }

            #endregion

            #region Constraint

            if (!schemaDesignerInfo.IgnoreTableConstraint)
                foreach (var constraintDesignerInfo in schemaDesignerInfo.TableConstraintDesignerInfos)
                {
                    var constraint = new TableConstraint
                        { Schema = constraintDesignerInfo.Schema, TableName = constraintDesignerInfo.TableName };
                    constraint.Name = constraintDesignerInfo.Name;
                    constraint.ColumnName = constraintDesignerInfo.ColumnName;
                    constraint.Definition = constraintDesignerInfo.Definition;
                    constraint.Comment = constraintDesignerInfo.Comment;

                    schemaInfo.TableConstraints.Add(constraint);
                }

            #endregion

            return schemaInfo;
        }

        private bool ValidateModel(SchemaDesignerInfo schemaDesignerInfo, out string message)
        {
            message = "";

            if (schemaDesignerInfo == null)
            {
                message = "Argument can't be null";
                return false;
            }

            var table = schemaDesignerInfo.TableDesignerInfo;

            if (table == null)
            {
                message = "No table information";
                return false;
            }

            if (string.IsNullOrEmpty(table.Name))
            {
                message = "Table Name can't be empty";
                return false;
            }

            #region Columns

            var columns = schemaDesignerInfo.TableColumnDesingerInfos;

            var columnNames = new List<string>();

            foreach (var column in columns)
            {
                if (string.IsNullOrEmpty(column.Name))
                {
                    message = "Column Name can't be empty";
                    return false;
                }

                if (string.IsNullOrEmpty(column.DataType))
                {
                    var computeExpression = column.ExtraPropertyInfo?.Expression;

                    if (string.IsNullOrEmpty(computeExpression) || dbInterpreter.DatabaseType == DatabaseType.MySql)
                    {
                        message = "Data Type can't be empty";
                        return false;
                    }
                }
                else if (columnNames.Contains(column.Name))
                {
                    message = $"Column Name \"{column.Name}\" is duplicated";
                    return false;
                }
                else if (!ColumnManager.ValidateDataType(dbInterpreter.DatabaseType, column, out message))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(column.Name) && !columnNames.Contains(column.Name))
                    columnNames.Add(column.Name);
            }

            #endregion

            #region Indexes

            if (!schemaDesignerInfo.IgnoreTableIndex)
            {
                var indexes = schemaDesignerInfo.TableIndexDesingerInfos;

                var indexNames = new List<string>();

                var clursteredCount = 0;

                foreach (var index in indexes)
                {
                    if (string.IsNullOrEmpty(index.Name))
                    {
                        message = "Index Name can't be empty";
                        return false;
                    }

                    if (indexNames.Contains(index.Name))
                    {
                        message = $"Index Name \"{index.Name}\" is duplicated";
                        return false;
                    }

                    if (index.Columns == null || index.Columns.Count == 0)
                    {
                        message = $"Index \"{index.Name}\" has no any column";
                        return false;
                    }

                    if (index.ExtraPropertyInfo != null)
                        if (index.ExtraPropertyInfo.Clustered)
                            clursteredCount++;

                    if (!string.IsNullOrEmpty(index.Name) && !indexNames.Contains(index.Name))
                        indexNames.Add(index.Name);
                }

                if (clursteredCount > 1)
                {
                    message = "The clurstered index count can't be more than one";
                    return false;
                }
            }

            #endregion

            #region Foreign Keys

            if (!schemaDesignerInfo.IgnoreTableForeignKey)
            {
                var foreignKeys = schemaDesignerInfo.TableForeignKeyDesignerInfos;

                var keyNames = new List<string>();

                foreach (var key in foreignKeys)
                {
                    if (string.IsNullOrEmpty(key.Name))
                    {
                        message = "Foreign Key Name can't be empty";
                        return false;
                    }

                    if (keyNames.Contains(key.Name))
                    {
                        message = $"Foreign Key Name \"{key.Name}\" is duplicated";
                        return false;
                    }

                    if (key.Columns == null || key.Columns.Count == 0)
                    {
                        message = $"The \"{key.Name}\" has no any column";
                        return false;
                    }

                    if (!string.IsNullOrEmpty(key.Name) && !keyNames.Contains(key.Name)) keyNames.Add(key.Name);
                }
            }

            #endregion

            #region Constraints

            if (!schemaDesignerInfo.IgnoreTableConstraint)
            {
                var constraints = schemaDesignerInfo.TableConstraintDesignerInfos;

                var constraintNames = new List<string>();
                var constraintColumnNames = new List<string>();

                foreach (var constraint in constraints)
                {
                    if (string.IsNullOrEmpty(constraint.Name) && dbInterpreter.DatabaseType != DatabaseType.Sqlite)
                    {
                        message = "Constraint Name can't be empty";
                        return false;
                    }

                    if (constraintNames.Contains(constraint.Name))
                    {
                        message = $"Constraint Name \"{constraint.Name}\" is duplicated";
                        return false;
                    }

                    if (string.IsNullOrEmpty(constraint.Definition))
                    {
                        message = "Constraint Expression can't be empty";
                        return false;
                    }

                    if (string.IsNullOrEmpty(constraint.ColumnName) &&
                        dbInterpreter.DatabaseType == DatabaseType.Sqlite)
                    {
                        message = "Column Name can't be empty";
                        return false;
                    }

                    if (!string.IsNullOrEmpty(constraint.ColumnName) &&
                        constraintColumnNames.Contains(constraint.ColumnName))
                    {
                        message = $"Column Name \"{constraint.ColumnName}\" is duplicated";
                        return false;
                    }

                    if (!string.IsNullOrEmpty(constraint.Name) && !constraintNames.Contains(constraint.Name))
                        constraintNames.Add(constraint.Name);

                    if (!string.IsNullOrEmpty(constraint.ColumnName) &&
                        !constraintColumnNames.Contains(constraint.ColumnName))
                        constraintColumnNames.Add(constraint.ColumnName);
                }
            }

            #endregion

            return true;
        }

        public void Feedback(FeedbackInfoType infoType, string message)
        {
            var info = new FeedbackInfo
                { Owner = this, InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(message) };

            if (observer != null) FeedbackHelper.Feedback(observer, info);
        }

        public void FeedbackInfo(string message)
        {
            Feedback(FeedbackInfoType.Info, message);
        }

        public void FeedbackError(string message)
        {
            Feedback(FeedbackInfoType.Error, message);
        }
    }
}