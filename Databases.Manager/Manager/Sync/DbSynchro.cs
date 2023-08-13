using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Databases.Interpreter;
using Databases.Interpreter.Utility.Helper;
using Databases.Interpreter.Utility.Model;
using Databases.Manager.Manager;
using Databases.Manager.Model;
using Databases.Manager.Model.DbObjectDisplay;
using Databases.Manager.Script;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Schema;
using Databases.Model.Script;
using Databases.ScriptGenerator;

namespace Databases.Manager.Sync
{
    public class DbSynchro
    {
        private readonly TableManager tableManager;
        private readonly DbInterpreter targetInterpreter;
        private readonly DbScriptGenerator targetScriptGenerator;
        private IObserver<FeedbackInfo> observer;
        private DbInterpreter sourceInterpreter;

        public DbSynchro(DbInterpreter sourceInterpreter, DbInterpreter targetInterpreter)
        {
            this.sourceInterpreter = sourceInterpreter;
            this.targetInterpreter = targetInterpreter;

            tableManager = new TableManager(this.targetInterpreter);
            targetScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(targetInterpreter);
        }

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public async Task<ContentSaveResult> Sync(SchemaInfo schemaInfo, string targetDbSchema,
            IEnumerable<DbDifference> differences)
        {
            var scripts = await GenerateChangedScripts(schemaInfo, targetDbSchema, differences);

            if (scripts == null || scripts.Count == 0)
            {
                return GetFaultSaveResult("No changes need to save.");
            }

            try
            {
                var scriptRunner = new ScriptRunner();

                await scriptRunner.Run(targetInterpreter, scripts);

                return new ContentSaveResult { IsOK = true };
            }
            catch (Exception ex)
            {
                var errMsg = ExceptionHelper.GetExceptionDetails(ex);

                FeedbackError(errMsg);

                return GetFaultSaveResult(errMsg);
            }
        }

        private ContentSaveResult GetFaultSaveResult(string message)
        {
            return new ContentSaveResult { ResultData = message };
        }

        public async Task<List<Databases.Model.Script.Script>> GenerateChangedScripts(SchemaInfo schemaInfo, string targetDbSchema,
            IEnumerable<DbDifference> differences)
        {
            var scripts = new List<Databases.Model.Script.Script>();
            var tableScripts = new List<Databases.Model.Script.Script>();

            foreach (var difference in differences)
            {
                var diffType = difference.DifferenceType;

                if (diffType == DbDifferenceType.None)
                {
                    continue;
                }

                switch (difference.DatabaseObjectType)
                {
                    case DatabaseObjectType.Table:
                        tableScripts.AddRange(
                            await GenerateTableChangedScripts(schemaInfo, difference, targetDbSchema));
                        break;
                    case DatabaseObjectType.View:
                    case DatabaseObjectType.Function:
                    case DatabaseObjectType.Procedure:
                        tableScripts.AddRange(GenereateScriptDbObjectChangedScripts(difference, targetDbSchema));
                        break;
                }
            }

            scripts.InsertRange(0, tableScripts);

            return scripts;
        }

        public List<Databases.Model.Script.Script> GenereateScriptDbObjectChangedScripts(DbDifference difference, string targetDbSchema)
        {
            var scripts = new List<Databases.Model.Script.Script>();

            var diffType = difference.DifferenceType;

            var sourceScriptDbObject = difference.Source as ScriptDbObject;
            var targetScriptDbObject = difference.Target as ScriptDbObject;

            if (diffType == DbDifferenceType.Added)
            {
                var cloneObj = CloneDbObject(sourceScriptDbObject, targetDbSchema);
                scripts.Add(new CreateDbObjectScript<ScriptDbObject>(cloneObj.Definition));
            }
            else if (diffType == DbDifferenceType.Deleted)
            {
                scripts.Add(targetScriptGenerator.Drop(sourceScriptDbObject));
            }
            else if (diffType == DbDifferenceType.Modified)
            {
                var cloneObj = CloneDbObject(sourceScriptDbObject, targetScriptDbObject.Schema);
                scripts.Add(targetScriptGenerator.Drop(targetScriptDbObject));
                scripts.Add(targetScriptGenerator.Create(cloneObj));
            }

            return scripts;
        }

        public List<Databases.Model.Script.Script> GenereateUserDefinedTypeChangedScripts(DbDifference difference, string targetDbSchema)
        {
            var scripts = new List<Databases.Model.Script.Script>();

            var diffType = difference.DifferenceType;

            var source = difference.Source as UserDefinedType;
            var target = difference.Target as UserDefinedType;

            if (diffType == DbDifferenceType.Added)
            {
                var cloneObj = CloneDbObject(source, targetDbSchema);
                scripts.Add(targetScriptGenerator.CreateUserDefinedType(cloneObj));
            }
            else if (diffType == DbDifferenceType.Deleted)
            {
                scripts.Add(targetScriptGenerator.DropUserDefinedType(source));
            }
            else if (diffType == DbDifferenceType.Modified)
            {
                var cloneObj = CloneDbObject(source, target.Schema);
                scripts.Add(targetScriptGenerator.DropUserDefinedType(target));
                scripts.Add(targetScriptGenerator.CreateUserDefinedType(cloneObj));
            }

            return scripts;
        }

        public async Task<List<Databases.Model.Script.Script>> GenerateTableChangedScripts(SchemaInfo schemaInfo, DbDifference difference,
            string targetDbSchema)
        {
            var scripts = new List<Databases.Model.Script.Script>();

            var diffType = difference.DifferenceType;

            var sourceTable = difference.Source as Table;
            var targetTable = difference.Target as Table;

            if (diffType == DbDifferenceType.Added)
            {
                var columns = schemaInfo.TableColumns
                    .Where(item => item.Schema == sourceTable.Schema && item.TableName == sourceTable.Name)
                    .OrderBy(item => item.Order).ToList();
                var primaryKey = schemaInfo.TablePrimaryKeys.FirstOrDefault(item =>
                    item.Schema == sourceTable.Schema && item.TableName == sourceTable.Name);
                var foreignKeys = schemaInfo.TableForeignKeys
                    .Where(item => item.Schema == sourceTable.Schema && item.TableName == sourceTable.Name).ToList();
                var indexes = schemaInfo.TableIndexes
                    .Where(item => item.Schema == sourceTable.Schema && item.TableName == sourceTable.Name)
                    .OrderBy(item => item.Order).ToList();
                var constraints = schemaInfo.TableConstraints
                    .Where(item => item.Schema == sourceTable.Schema && item.TableName == sourceTable.Name).ToList();

                ChangeSchema(columns, targetDbSchema);
                primaryKey = CloneDbObject(primaryKey, targetDbSchema);
                ChangeSchema(foreignKeys, targetDbSchema);
                ChangeSchema(indexes, targetDbSchema);
                ChangeSchema(constraints, targetDbSchema);

                scripts.AddRange(targetScriptGenerator
                    .CreateTable(sourceTable, columns, primaryKey, foreignKeys, indexes, constraints).Scripts);
            }
            else if (diffType == DbDifferenceType.Deleted)
            {
                scripts.Add(targetScriptGenerator.DropTable(targetTable));
            }
            else if (diffType == DbDifferenceType.Modified)
            {
                if (!ValueHelper.IsStringEquals(sourceTable.Comment, targetTable.Comment))
                {
                    scripts.Add(targetScriptGenerator.SetTableComment(sourceTable,
                        string.IsNullOrEmpty(targetTable.Comment)));
                }

                foreach (var subDiff in difference.SubDifferences)
                {
                    var subDiffType = subDiff.DifferenceType;

                    if (subDiffType == DbDifferenceType.None)
                    {
                        continue;
                    }

                    var subDbObjectType = subDiff.DatabaseObjectType;

                    switch (subDbObjectType)
                    {
                        case DatabaseObjectType.Column:
                        case DatabaseObjectType.PrimaryKey:
                        case DatabaseObjectType.ForeignKey:
                        case DatabaseObjectType.Index:
                        case DatabaseObjectType.Constraint:
                            scripts.AddRange(await GenerateTableChildChangedScripts(subDiff));
                            break;

                        case DatabaseObjectType.Trigger:
                            scripts.AddRange(GenereateScriptDbObjectChangedScripts(subDiff, targetDbSchema));
                            break;
                    }
                }
            }

            return scripts;
        }

        public async Task<List<Databases.Model.Script.Script>> GenerateTableChildChangedScripts(DbDifference difference)
        {
            var scripts = new List<Databases.Model.Script.Script>();

            var targetTable = difference.Parent.Target as Table;

            var diffType = difference.DifferenceType;

            var source = difference.Source as TableChild;
            var target = difference.Target as TableChild;

            if (diffType == DbDifferenceType.Added)
            {
                scripts.Add(targetScriptGenerator.Create(CloneTableChild(source, difference.DatabaseObjectType,
                    targetTable.Schema)));
            }
            else if (diffType == DbDifferenceType.Deleted)
            {
                scripts.Add(targetScriptGenerator.Drop(target));
            }
            else if (diffType == DbDifferenceType.Modified)
            {
                if (difference.DatabaseObjectType == DatabaseObjectType.Column)
                {
                    var filter = new SchemaInfoFilter
                        { Schema = source.Schema, TableNames = new[] { source.TableName } };
                    var defaultValueConstraints = await tableManager.GetTableDefaultConstraints(filter);

                    var table = new Table { Schema = targetTable.Schema, Name = target.TableName };

                    var sourceColumn = source as TableColumn;
                    var targetColumn = target as TableColumn;

                    if (tableManager.IsNameChanged(sourceColumn.Name, targetColumn.Name))
                    {
                        scripts.Add(tableManager.GetColumnRenameScript(table, sourceColumn, targetColumn));
                    }

                    scripts.AddRange(tableManager.GetColumnAlterScripts(table, table, targetColumn, sourceColumn,
                        defaultValueConstraints));
                }
                else
                {
                    var clonedSource = CloneTableChild(difference.Source, difference.DatabaseObjectType,
                        targetTable.Schema);

                    if (difference.DatabaseObjectType == DatabaseObjectType.PrimaryKey)
                    {
                        scripts.AddRange(tableManager.GetPrimaryKeyAlterScripts(target as TablePrimaryKey,
                            clonedSource as TablePrimaryKey, false));
                    }
                    else if (difference.DatabaseObjectType == DatabaseObjectType.ForeignKey)
                    {
                        scripts.AddRange(tableManager.GetForeignKeyAlterScripts(target as TableForeignKey,
                            clonedSource as TableForeignKey));
                    }
                    else if (difference.DatabaseObjectType == DatabaseObjectType.Index)
                    {
                        scripts.AddRange(tableManager.GetIndexAlterScripts(target as TableIndex,
                            clonedSource as TableIndex));
                    }
                    else if (difference.DatabaseObjectType == DatabaseObjectType.Constraint)
                    {
                        scripts.AddRange(tableManager.GetConstraintAlterScripts(target as TableConstraint,
                            clonedSource as TableConstraint));
                    }
                }
            }

            return scripts;
        }

        private DatabaseObject CloneTableChild(DatabaseObject tableChild, DatabaseObjectType databaseObjectType,
            string targetSchema)
        {
            if (databaseObjectType == DatabaseObjectType.PrimaryKey)
            {
                return CloneDbObject(tableChild as TablePrimaryKey, targetSchema);
            }

            if (databaseObjectType == DatabaseObjectType.ForeignKey)
            {
                return CloneDbObject(tableChild as TableForeignKey, targetSchema);
            }

            if (databaseObjectType == DatabaseObjectType.Index)
            {
                return CloneDbObject(tableChild as TableIndex, targetSchema);
            }

            if (databaseObjectType == DatabaseObjectType.Constraint)
            {
                return CloneDbObject(tableChild as TableConstraint, targetSchema);
            }

            return tableChild;
        }

        private T CloneDbObject<T>(T dbObject, string owner) where T : DatabaseObject
        {
            if (dbObject == null)
            {
                return null;
            }

            var clonedObj = ObjectHelper.CloneObject<T>(dbObject);
            clonedObj.Schema = owner;

            return clonedObj;
        }

        private void ChangeSchema<T>(List<T> dbObjects, string schema) where T : DatabaseObject
        {
            dbObjects.ForEach(item => item = CloneDbObject(item, schema));
        }

        public void Feedback(FeedbackInfoType infoType, string message)
        {
            var info = new FeedbackInfo
                { Owner = this, InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(message) };

            if (observer != null)
            {
                FeedbackHelper.Feedback(observer, info);
            }
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