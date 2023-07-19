using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;
using KellermanSoftware.CompareNetObjects;

namespace DatabaseManager.Core
{
    public class DbCompare
    {
        private readonly SchemaInfo sourceSchemaInfo;
        private readonly SchemaInfo targetSchemaInfo;

        public DbCompare(SchemaInfo sourceSchemaInfo, SchemaInfo targetSchemaInfo)
        {
            this.sourceSchemaInfo = sourceSchemaInfo;
            this.targetSchemaInfo = targetSchemaInfo;
        }

        public List<DbDifference> Compare()
        {
            var differences = new List<DbDifference>();

            differences.AddRange(CompareDatabaseObjects(nameof(UserDefinedType), DatabaseObjectType.Type,
                sourceSchemaInfo.UserDefinedTypes, targetSchemaInfo.UserDefinedTypes));

            #region Table

            foreach (var target in targetSchemaInfo.Tables)
            {
                var difference = new DbDifference
                    { Type = nameof(Table), DatabaseObjectType = DatabaseObjectType.Table };

                var source = sourceSchemaInfo.Tables.FirstOrDefault(item => IsNameEquals(item.Name, target.Name));

                if (source == null)
                {
                    difference.DifferenceType = DbDifferenceType.Deleted;
                    difference.Target = target;

                    differences.Add(difference);
                }
                else
                {
                    difference.DifferenceType = DbDifferenceType.None;
                    difference.Source = source;
                    difference.Target = target;

                    differences.Add(difference);

                    var isTableEquals = IsDbObjectEquals(source, target);

                    if (isTableEquals)
                    {
                        #region Column

                        var sourceColumns = sourceSchemaInfo.TableColumns.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetColumns = targetSchemaInfo.TableColumns.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var columnDifferences = CompareTableChildren("Column", DatabaseObjectType.Column, sourceColumns,
                            targetColumns);

                        difference.SubDifferences.AddRange(columnDifferences);

                        #endregion

                        #region Trigger

                        var sourceTriggers = sourceSchemaInfo.TableTriggers.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetTriggers = targetSchemaInfo.TableTriggers.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var triggerDifferences = CompareDatabaseObjects("Trigger", DatabaseObjectType.Trigger,
                            sourceTriggers, targetTriggers);

                        foreach (var triggerDiff in triggerDifferences) triggerDiff.ParentName = target.Name;

                        difference.SubDifferences.AddRange(triggerDifferences);

                        #endregion

                        #region Index

                        var sourceIndexes = sourceSchemaInfo.TableIndexes.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetIndexes = targetSchemaInfo.TableIndexes.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var indexDifferences = CompareTableChildren("Index", DatabaseObjectType.Index, sourceIndexes,
                            targetIndexes);

                        difference.SubDifferences.AddRange(indexDifferences);

                        #endregion

                        #region Primary Key

                        var sourcePrimaryKeys = sourceSchemaInfo.TablePrimaryKeys.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetPrimaryKeys = targetSchemaInfo.TablePrimaryKeys.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var primaryKeyDifferences = CompareTableChildren("Primary Key", DatabaseObjectType.PrimaryKey,
                            sourcePrimaryKeys, targetPrimaryKeys);

                        difference.SubDifferences.AddRange(primaryKeyDifferences);

                        #endregion

                        #region Foreign Key

                        var sourceForeignKeys = sourceSchemaInfo.TableForeignKeys.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetForeignKeys = targetSchemaInfo.TableForeignKeys.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var foreignKeyDifferences = CompareTableChildren("Foreign Key", DatabaseObjectType.ForeignKey,
                            sourceForeignKeys, targetForeignKeys);

                        difference.SubDifferences.AddRange(foreignKeyDifferences);

                        #endregion

                        #region Constraint

                        var sourceConstraints = sourceSchemaInfo.TableConstraints.Where(item =>
                            item.Schema == source.Schema && item.TableName == source.Name);
                        var targetConstraints = targetSchemaInfo.TableConstraints.Where(item =>
                            item.Schema == target.Schema && item.TableName == source.Name);

                        var constraintDifferences = CompareTableChildren("Constraint", DatabaseObjectType.Constraint,
                            sourceConstraints, targetConstraints);

                        difference.SubDifferences.AddRange(constraintDifferences);

                        #endregion

                        difference.SubDifferences.ForEach(item => item.Parent = difference);

                        if (difference.SubDifferences.Any(item => item.DifferenceType != DbDifferenceType.None))
                            difference.DifferenceType = DbDifferenceType.Modified;
                    }
                }
            }

            foreach (var source in sourceSchemaInfo.Tables)
                if (!targetSchemaInfo.Tables.Any(item => IsNameEquals(item.Name, source.Name)))
                {
                    var difference = new DbDifference
                        { Type = nameof(Table), DatabaseObjectType = DatabaseObjectType.Table };
                    difference.DifferenceType = DbDifferenceType.Added;
                    difference.Source = source;

                    differences.Add(difference);
                }

            #endregion

            differences.AddRange(CompareDatabaseObjects(nameof(View), DatabaseObjectType.View, sourceSchemaInfo.Views,
                targetSchemaInfo.Views));
            differences.AddRange(CompareDatabaseObjects(nameof(Function), DatabaseObjectType.Function,
                sourceSchemaInfo.Functions, targetSchemaInfo.Functions));
            differences.AddRange(CompareDatabaseObjects(nameof(Procedure), DatabaseObjectType.Procedure,
                sourceSchemaInfo.Procedures, targetSchemaInfo.Procedures));

            return differences;
        }

        private List<DbDifference> CompareTableChildren<T>(string type, DatabaseObjectType databaseObjectType,
            IEnumerable<T> sourceObjects, IEnumerable<T> targetObjects)
            where T : TableChild
        {
            var differences = new List<DbDifference>();

            foreach (var target in targetObjects)
            {
                var difference = new DbDifference
                    { Type = type, DatabaseObjectType = databaseObjectType, ParentName = target.TableName };

                var source = sourceObjects.FirstOrDefault(item => IsNameEquals(item.Name, target.Name));

                if (source == null)
                {
                    difference.DifferenceType = DbDifferenceType.Deleted;
                    difference.Target = target;

                    differences.Add(difference);
                }
                else
                {
                    difference.Source = source;
                    difference.Target = target;

                    if (!IsDbObjectEquals(source, target)) difference.DifferenceType = DbDifferenceType.Modified;

                    differences.Add(difference);
                }
            }

            foreach (var source in sourceObjects)
                if (!targetObjects.Any(item => IsNameEquals(item.Name, source.Name)))
                {
                    var difference = new DbDifference
                        { Type = type, DatabaseObjectType = databaseObjectType, ParentName = source.TableName };
                    difference.DifferenceType = DbDifferenceType.Added;
                    difference.Source = source;

                    differences.Add(difference);
                }

            return differences;
        }

        private List<DbDifference> CompareDatabaseObjects<T>(string type, DatabaseObjectType databaseObjectType,
            IEnumerable<T> sourceObjects, IEnumerable<T> targetObjects)
            where T : DatabaseObject
        {
            var differences = new List<DbDifference>();

            foreach (var target in targetObjects)
            {
                var difference = new DbDifference { Type = type, DatabaseObjectType = databaseObjectType };

                var source = sourceObjects.FirstOrDefault(item => IsNameEquals(item.Name, target.Name));

                if (source == null)
                {
                    difference.DifferenceType = DbDifferenceType.Deleted;
                    difference.Target = target;

                    differences.Add(difference);
                }
                else
                {
                    difference.Source = source;
                    difference.Target = target;

                    if (!IsDbObjectEquals(source, target)) difference.DifferenceType = DbDifferenceType.Modified;

                    differences.Add(difference);
                }
            }

            foreach (var source in sourceObjects)
                if (!targetObjects.Any(item => IsNameEquals(item.Name, source.Name)))
                {
                    var difference = new DbDifference { Type = type, DatabaseObjectType = databaseObjectType };
                    difference.DifferenceType = DbDifferenceType.Added;
                    difference.Source = source;

                    differences.Add(difference);
                }

            return differences;
        }

        private bool IsNameEquals(string name1, string name2)
        {
            return name1.ToLower() == name2.ToLower();
        }

        private bool IsDbObjectEquals(DatabaseObject source, DatabaseObject target)
        {
            var config = new ComparisonConfig();
            config.MembersToIgnore = new List<string> { nameof(DatabaseObject.Schema), nameof(DatabaseObject.Order) };
            config.CaseSensitive = false;
            config.IgnoreStringLeadingTrailingWhitespace = true;
            config.TreatStringEmptyAndNullTheSame = true;
            config.CustomComparers.Add(new TableColumnComparer(RootComparerFactory.GetRootComparer()));

            var compareLogic = new CompareLogic(config);

            var result = compareLogic.Compare(source, target);

            return result.AreEqual;
        }
    }
}