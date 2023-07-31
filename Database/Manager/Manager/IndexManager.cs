using System.Collections.Generic;
using Databases.Interpreter.Utility.Helper;
using Databases.Manager.Model.TableDesigner;
using Databases.Model.DatabaseObject;
using Databases.Model.Enum;

namespace Databases.Manager.Manager
{
    public class IndexManager
    {
        public static string GetPrimaryKeyDefaultName(Table table)
        {
            return $"PK_{table.Name}";
        }

        public static string GetIndexDefaultName(string indexType, Table table)
        {
            return $"{(indexType == nameof(IndexType.Unique) ? "UK" : "IX")}_{table.Name}";
        }

        public static string GetForeignKeyDefaultName(string tableName, string referencedTableName)
        {
            return $"FK_{referencedTableName}_{tableName}";
        }

        public static List<TableIndexDesignerInfo> GetIndexDesignerInfos(DatabaseType databaseType,
            List<TableIndex> indexes)
        {
            var indexDesignerInfos = new List<TableIndexDesignerInfo>();

            foreach (var index in indexes)
            {
                var indexDesignerInfo = new TableIndexDesignerInfo();

                indexDesignerInfo.OldName = indexDesignerInfo.Name = index.Name;
                indexDesignerInfo.IsPrimary = index.IsPrimary;
                indexDesignerInfo.OldType = index.Type;
                indexDesignerInfo.Comment = index.Comment;

                var type = index.Type;

                if (!string.IsNullOrEmpty(type))
                {
                    indexDesignerInfo.Type = type;
                }

                if (index.IsPrimary)
                {
                    if (databaseType == DatabaseType.Oracle)
                    {
                        indexDesignerInfo.Type = IndexType.Unique.ToString();
                    }
                    else
                    {
                        indexDesignerInfo.Type = IndexType.Primary.ToString();
                    }

                    if (indexDesignerInfo.ExtraPropertyInfo == null)
                    {
                        indexDesignerInfo.ExtraPropertyInfo = new TableIndexExtraPropertyInfo();
                    }

                    indexDesignerInfo.ExtraPropertyInfo.Clustered = index.Clustered;
                }
                else if (index.IsUnique)
                {
                    indexDesignerInfo.Type = IndexType.Unique.ToString();
                }
                else if (string.IsNullOrEmpty(index.Type))
                {
                    indexDesignerInfo.Type = IndexType.Normal.ToString();
                }

                if (string.IsNullOrEmpty(indexDesignerInfo.OldType) && !string.IsNullOrEmpty(indexDesignerInfo.Type))
                {
                    indexDesignerInfo.OldType = indexDesignerInfo.Type;
                }

                indexDesignerInfo.Columns.AddRange(index.Columns);

                indexDesignerInfos.Add(indexDesignerInfo);
            }

            return indexDesignerInfos;
        }

        public static List<TableForeignKeyDesignerInfo> GetForeignKeyDesignerInfos(List<TableForeignKey> foreignKeys)
        {
            var foreignKeyDesignerInfos = new List<TableForeignKeyDesignerInfo>();

            foreach (var foreignKey in foreignKeys)
            {
                var keyDesignerInfo = new TableForeignKeyDesignerInfo();

                ObjectHelper.CopyProperties(foreignKey, keyDesignerInfo);

                keyDesignerInfo.OldName = foreignKey.Name;
                keyDesignerInfo.Columns = foreignKey.Columns;

                foreignKeyDesignerInfos.Add(keyDesignerInfo);
            }

            return foreignKeyDesignerInfos;
        }

        public static List<TableConstraintDesignerInfo> GetConstraintDesignerInfos(List<TableConstraint> constraints)
        {
            var constraintDesignerInfos = new List<TableConstraintDesignerInfo>();

            foreach (var constraint in constraints)
            {
                var constraintDesignerInfo = new TableConstraintDesignerInfo();

                ObjectHelper.CopyProperties(constraint, constraintDesignerInfo);

                constraintDesignerInfo.OldName = constraint.Name;

                constraintDesignerInfos.Add(constraintDesignerInfo);
            }

            return constraintDesignerInfos;
        }
    }
}