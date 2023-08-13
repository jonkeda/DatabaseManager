using System.Collections.Generic;
using System.Linq;
using Databases.Model.DatabaseObject;

namespace Databases.Interpreter.Helper
{
    public class TableReferenceHelper
    {
        public static IEnumerable<string> GetTopReferencedTableNames(IEnumerable<TableForeignKey> tableForeignKeys)
        {
            var foreignTableNames = tableForeignKeys.Select(item => item.TableName);

            IEnumerable<string> topReferencedTableNames = tableForeignKeys.Where(item =>
                    !foreignTableNames.Contains(item.ReferencedTableName)
                    || (item.TableName == item.ReferencedTableName && tableForeignKeys.Any(t =>
                                                                       t.Name != item.Name &&
                                                                       item.TableName == t.ReferencedTableName)
                                                                   && !tableForeignKeys.Any(t =>
                                                                       t.Name != item.Name &&
                                                                       item.ReferencedTableName == t.TableName)))
                .Select(item => item.ReferencedTableName).Distinct().OrderBy(item => item);

            return topReferencedTableNames;
        }


        public static List<string> ResortTableNames(string[] tableNames, List<TableForeignKey> tableForeignKeys)
        {
            var sortedTableNames = new List<string>();
            var primaryTableNames = tableForeignKeys.Select(item => item.ReferencedTableName);
            var foreignTableNames = tableForeignKeys.Select(item => item.TableName);

            IEnumerable<string> notReferencedTableNames = tableNames
                .Where(item => !primaryTableNames.Contains(item) && !foreignTableNames.Contains(item))
                .OrderBy(item => item);

            sortedTableNames.AddRange(notReferencedTableNames);

            var topReferencedTableNames = GetTopReferencedTableNames(tableForeignKeys);

            sortedTableNames.AddRange(topReferencedTableNames);

            var childTableNames = new List<string>();

            foreach (var tableName in topReferencedTableNames)
            {
                childTableNames.AddRange(GetForeignTables(tableName, tableForeignKeys,
                    sortedTableNames.Concat(childTableNames)));
            }

            var sortedChildTableNames = GetSortedTableNames(childTableNames, tableForeignKeys);

            sortedChildTableNames = sortedChildTableNames.Distinct().ToList();

            sortedTableNames.AddRange(sortedChildTableNames);

            IEnumerable<string> selfReferencedTableNames = tableForeignKeys
                .Where(item => item.TableName == item.ReferencedTableName)
                .Select(item => item.TableName).OrderBy(item => item);

            sortedTableNames.AddRange(selfReferencedTableNames.Where(item => !sortedTableNames.Contains(item)));

            return sortedTableNames;
        }

        private static List<string> GetSortedTableNames(List<string> tableNames, List<TableForeignKey> tableForeignKeys)
        {
            var sortedTableNames = new List<string>();

            for (var i = 0; i <= tableNames.Count - 1; i++)
            {
                var tableName = tableNames[i];

                var foreignKeys = tableForeignKeys.Where(item =>
                    item.TableName == tableName && item.TableName != item.ReferencedTableName);

                if (foreignKeys.Any())
                {
                    foreach (var foreignKey in foreignKeys)
                    {
                        var referencedTableIndex = tableNames.IndexOf(foreignKey.ReferencedTableName);

                        if (referencedTableIndex >= 0 && referencedTableIndex > i)
                        {
                            sortedTableNames.Add(foreignKey.ReferencedTableName);
                        }
                    }
                }

                sortedTableNames.Add(tableName);
            }

            sortedTableNames = sortedTableNames.Distinct().ToList();

            var needSort = false;

            for (var i = 0; i <= sortedTableNames.Count - 1; i++)
            {
                var tableName = sortedTableNames[i];

                var foreignKeys = tableForeignKeys.Where(item =>
                    item.TableName == tableName && item.TableName != item.ReferencedTableName);

                if (foreignKeys.Any())
                {
                    foreach (var foreignKey in foreignKeys)
                    {
                        var referencedTableIndex = sortedTableNames.IndexOf(foreignKey.ReferencedTableName);

                        if (referencedTableIndex >= 0 && referencedTableIndex > i)
                        {
                            needSort = true;
                            break;
                        }
                    }
                }

                if (needSort)
                {
                    return GetSortedTableNames(sortedTableNames, tableForeignKeys);
                }
            }

            return sortedTableNames;
        }

        private static List<string> GetForeignTables(string tableName, List<TableForeignKey> tableForeignKeys,
            IEnumerable<string> sortedTableNames)
        {
            var tableNames = new List<string>();

            var foreignTableNames = tableForeignKeys
                .Where(item => item.ReferencedTableName == tableName && item.TableName != tableName &&
                               !sortedTableNames.Contains(item.TableName)).Select(item => item.TableName);

            tableNames.AddRange(foreignTableNames);

            var childForeignTableNames = tableForeignKeys
                .Where(item => foreignTableNames.Contains(item.ReferencedTableName)).Select(item => item.TableName);

            if (childForeignTableNames.Any())
            {
                var childNames = foreignTableNames
                    .SelectMany(item => GetForeignTables(item, tableForeignKeys, sortedTableNames)).ToList();
                tableNames.AddRange(childNames.Where(item => !tableNames.Contains(item)));
            }

            return tableNames;
        }

        public static bool IsSelfReference(string tableName, List<TableForeignKey> tableForeignKeys)
        {
            return tableForeignKeys.Any(item =>
                item.TableName == tableName && item.TableName == item.ReferencedTableName);
        }

        public static List<Table> ResortTables(List<Table> tables, List<TableForeignKey> foreignKeys)
        {
            var tableNames = tables.Select(item => item.Name).ToArray();

            var sortedTableNames = ResortTableNames(tableNames, foreignKeys);

            var i = 1;

            foreach (var tableName in sortedTableNames)
            {
                var table = tables.FirstOrDefault(item => item.Name == tableName);

                if (table != null)
                {
                    table.Order = i++;
                }
            }

            return tables.OrderBy(item => item.Order).ToList();
        }
    }
}