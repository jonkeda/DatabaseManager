﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Databases.Model.DatabaseObject;
using Databases.Model.DatabaseObject.Fiction;
using Databases.Model.Schema;

namespace Databases.Interpreter.Helper
{
    public class DbObjectHelper
    {
        public static void Resort<T>(List<T> dbObjects)
            where T : ScriptDbObject
        {
            for (var i = 0; i < dbObjects.Count - 1; i++)
            for (var j = i + 1; j < dbObjects.Count - 1; j++)
            {
                if (!string.IsNullOrEmpty(dbObjects[i].Definition))
                {
                    var nameRegex = new Regex($"\\b({dbObjects[j].Name})\\b", RegexOptions.IgnoreCase);

                    if (nameRegex.IsMatch(dbObjects[i].Definition))
                    {
                        var temp = dbObjects[j];
                        dbObjects[j] = dbObjects[i];
                        dbObjects[i] = temp;
                    }
                }
            }
        }

        public static List<TableColumn> ResortTableColumns(IEnumerable<Table> tables, List<TableColumn> columns)
        {
            if (!tables.Any())
            {
                return columns;
            }

            var sortedColumns = new List<TableColumn>();

            foreach (var table in tables)
            {
                sortedColumns.AddRange(columns
                    .Where(item => item.Schema == table.Schema && item.TableName == table.Name)
                    .OrderBy(item => item.Order));
            }

            if (sortedColumns.Count < columns.Count)
            {
                sortedColumns.AddRange(columns.Where(item => !sortedColumns.Contains(item)));
            }

            return sortedColumns;
        }

        public static DatabaseObjectType GetDatabaseObjectType(DatabaseObject dbObject)
        {
            var typeName = dbObject.GetType().Name;

            if (typeName == nameof(TablePrimaryKeyItem))
            {
                return DatabaseObjectType.PrimaryKey;
            }

            if (typeName == nameof(TableForeignKeyItem))
            {
                return DatabaseObjectType.ForeignKey;
            }

            if (typeName.StartsWith(nameof(Table)) && typeName != nameof(Table))
            {
                typeName = typeName.Replace(nameof(Table), "");
            }

            if (typeName == nameof(UserDefinedType))
            {
                return DatabaseObjectType.Type;
            }

            if (Enum.TryParse<DatabaseObjectType>(typeName, out DatabaseObjectType databaseObjectType))
            {
                return databaseObjectType;
            }

            return DatabaseObjectType.None;
        }
    }
}