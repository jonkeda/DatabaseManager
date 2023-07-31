using System;
using System.Collections.Generic;
using Databases.Manager.Helper;
using Databases.Model.Enum;
using Databases.Model.Schema;

namespace DatabaseManager.Helper;

public class CheckItemInfo
{
    public string Name { get; set; }
    public bool Checked { get; set; }
}

public class ItemsSelectorHelper
{
    public static List<CheckItemInfo> GetDatabaseObjectTypeItems(DatabaseType databaseType,
        DatabaseObjectType supportDatabaseObjectType = DatabaseObjectType.None)
    {
        var dbObjTypes = new List<DatabaseObjectType>
        {
            DatabaseObjectType.Trigger,
            DatabaseObjectType.Table,
            DatabaseObjectType.View,
            DatabaseObjectType.Function,
            DatabaseObjectType.Procedure,
            DatabaseObjectType.Type,
            DatabaseObjectType.Sequence
        };

        var checkItems = new List<CheckItemInfo>();

        if (supportDatabaseObjectType != DatabaseObjectType.None)
            foreach (var dbObjType in dbObjTypes)
                if (dbObjType == DatabaseObjectType.Trigger || supportDatabaseObjectType.HasFlag(dbObjType))
                    checkItems.Add(new CheckItemInfo
                        { Name = ManagerUtil.GetPluralString(dbObjType.ToString()), Checked = true });

        return checkItems;
    }

    public static DatabaseObjectType GetDatabaseObjectTypeByCheckItems(List<CheckItemInfo> items)
    {
        var databaseObjectType = DatabaseObjectType.None;

        foreach (var item in items)
        {
            var type = (DatabaseObjectType)Enum.Parse(typeof(DatabaseObjectType),
                ManagerUtil.GetSingularString(item.Name));

            databaseObjectType = databaseObjectType | type;
        }

        return databaseObjectType;
    }

    public static List<CheckItemInfo> GetDatabaseTypeItems(List<string> databaseTypes, bool checkedIfNotConfig = true)
    {
        var items = new List<CheckItemInfo>();

        var dbTypes = Enum.GetNames(typeof(DatabaseType));

        foreach (var dbType in dbTypes)
            if (dbType != nameof(DatabaseType.Unknown))
            {
                var @checked = (checkedIfNotConfig && databaseTypes.Count == 0) || databaseTypes.Contains(dbType);

                items.Add(new CheckItemInfo { Name = dbType, Checked = @checked });
            }

        return items;
    }
}