using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    public class DepencencyFetcher
    {
        private readonly DatabaseType databaseType;
        private readonly DbInterpreter dbInterpreter;

        public DepencencyFetcher(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
            databaseType = this.dbInterpreter.DatabaseType;
        }

        public async Task<List<DbObjectUsage>> Fetch(DatabaseObject dbObject, bool denpendOnThis = true)
        {
            var usages = new List<DbObjectUsage>();

            var objType = DbObjectHelper.GetDatabaseObjectType(dbObject);

            #region Table Dependencies

            if (objType == DatabaseObjectType.Table)
            {
                var tableForeignKeysFilter = GetTableSchemaInfoFilter(dbObject);

                var foreignKeys = await dbInterpreter.GetTableForeignKeysAsync(tableForeignKeysFilter, denpendOnThis);

                var fkGroups = foreignKeys.GroupBy(item => new
                        { item.Schema, item.TableName, item.ReferencedSchema, item.ReferencedTableName })
                    .OrderBy(item => item.Key.Schema).ThenBy(item => item.Key.TableName);

                foreach (var fk in fkGroups)
                {
                    var usage = new DbObjectUsage { ObjectType = "Table", RefObjectType = "Table" };

                    if (denpendOnThis)
                    {
                        usage.ObjectSchema = fk.Key.Schema;
                        usage.ObjectName = fk.Key.TableName;
                        usage.RefObjectSchema = dbObject.Schema;
                        usage.RefObjectName = dbObject.Name;
                    }
                    else
                    {
                        usage.ObjectSchema = dbObject.Schema;
                        usage.ObjectName = dbObject.Name;
                        usage.RefObjectSchema = fk.Key.ReferencedSchema;
                        usage.RefObjectName = fk.Key.ReferencedTableName;
                    }

                    usages.Add(usage);
                }
            }

            #endregion

            #region View Dependencies

            if (objType == DatabaseObjectType.Table || objType == DatabaseObjectType.View)
            {
                var viewTablesFilter =
                    denpendOnThis ? GetTableSchemaInfoFilter(dbObject) : GetViewSchemaInfoFilter(dbObject);

                var viewTableUsages = await dbInterpreter.GetViewTableUsages(viewTablesFilter, denpendOnThis);

                usages.AddRange(viewTableUsages);
            }

            #endregion

            #region Routine Script Dependencies

            if (databaseType == DatabaseType.SqlServer || databaseType == DatabaseType.Oracle)
            {
                var routineScriptsFilter = GetDbObjectSchemaInfoFilter(dbObject);
                routineScriptsFilter.DatabaseObjectType = objType;

                var routineScriptUsages =
                    await dbInterpreter.GetRoutineScriptUsages(routineScriptsFilter, denpendOnThis);

                usages.AddRange(routineScriptUsages);
            }
            else
            {
                using (var connection = dbInterpreter.CreateConnection())
                {
                    if (denpendOnThis)
                    {
                        dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Details;

                        var procedures = await dbInterpreter.GetProceduresAsync(connection);

                        if (!(dbObject is Procedure))
                        {
                            var functions = await dbInterpreter.GetFunctionsAsync(connection);
                            usages.AddRange(GetRoutineScriptUsagesForRef(functions, dbObject));
                        }

                        usages.AddRange(GetRoutineScriptUsagesForRef(procedures, dbObject));
                    }
                    else
                    {
                        dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Details;

                        var dbObjectFilter = GetDbObjectSchemaInfoFilter(dbObject);

                        ScriptDbObject sdb = null;

                        if (dbObject is View)
                            sdb = (await dbInterpreter.GetViewsAsync(connection, dbObjectFilter)).FirstOrDefault();
                        else if (dbObject is Function)
                            sdb = (await dbInterpreter.GetFunctionsAsync(connection, dbObjectFilter)).FirstOrDefault();
                        else if (dbObject is Procedure)
                            sdb = (await dbInterpreter.GetProceduresAsync(connection, dbObjectFilter)).FirstOrDefault();

                        dbInterpreter.Option.ObjectFetchMode = DatabaseObjectFetchMode.Simple;

                        var functions = await dbInterpreter.GetFunctionsAsync(connection);
                        var routineScriptUsages = new List<RoutineScriptUsage>();

                        if (sdb is View)
                        {
                            routineScriptUsages.AddRange(GetRoutineScriptUsages(sdb, functions));
                        }
                        else if (sdb is Function || sdb is Procedure)
                        {
                            var tables = await dbInterpreter.GetTablesAsync(connection);
                            var views = await dbInterpreter.GetViewsAsync(connection);

                            routineScriptUsages.AddRange(GetRoutineScriptUsages(sdb, tables));
                            routineScriptUsages.AddRange(GetRoutineScriptUsages(sdb, views));
                            routineScriptUsages.AddRange(GetRoutineScriptUsages(sdb, functions));

                            if (sdb is Procedure)
                            {
                                var procedures = await dbInterpreter.GetProceduresAsync(connection);

                                routineScriptUsages.AddRange(GetRoutineScriptUsages(sdb, procedures));
                            }
                        }

                        if (routineScriptUsages.Count > 0) usages.AddRange(routineScriptUsages);
                    }
                }
            }

            #endregion

            return usages;
        }

        private List<RoutineScriptUsage> GetRoutineScriptUsages(ScriptDbObject scriptDbObject,
            IEnumerable<DatabaseObject> dbObjects)
        {
            var usages = new List<RoutineScriptUsage>();

            var dbObjectNames = dbObjects
                .Where(item => !(item.Schema == scriptDbObject.Schema && item.Name == scriptDbObject.Name))
                .Select(item => item.Name);

            foreach (var name in dbObjectNames)
            {
                var body = ScriptParser.ExtractScriptBody(scriptDbObject.Definition);

                if (Regex.IsMatch(body, $@"\b{name}\b", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    var usage = new RoutineScriptUsage
                    {
                        ObjectType = scriptDbObject.GetType().Name, ObjectSchema = scriptDbObject.Schema,
                        ObjectName = scriptDbObject.Name
                    };

                    var dbObj = dbObjects.FirstOrDefault(item => item.Name == name);

                    usage.RefObjectType = dbObj.GetType().Name;
                    usage.RefObjectSchema = dbObj.Schema;
                    usage.RefObjectName = dbObj.Name;

                    usages.Add(usage);
                }
            }

            return usages;
        }

        private List<RoutineScriptUsage> GetRoutineScriptUsagesForRef(IEnumerable<ScriptDbObject> scriptDbObjects,
            DatabaseObject refDbObject)
        {
            var usages = new List<RoutineScriptUsage>();

            foreach (var sdb in scriptDbObjects.Where(item =>
                         !(item.Schema == refDbObject.Schema && item.Name == refDbObject.Name)))
                if (Regex.IsMatch(sdb.Definition, $@"\b{refDbObject.Name}\b",
                        RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    var usage = new RoutineScriptUsage
                        { ObjectType = sdb.GetType().Name, ObjectSchema = sdb.Schema, ObjectName = sdb.Name };

                    usage.RefObjectType = refDbObject.GetType().Name;
                    usage.RefObjectSchema = refDbObject.Schema;
                    usage.RefObjectName = refDbObject.Name;

                    usages.Add(usage);
                }

            return usages;
        }

        private SchemaInfoFilter GetSchemaInfoFilter(DatabaseObject dbObject)
        {
            var filter = new SchemaInfoFilter { Schema = dbObject.Schema };

            return filter;
        }

        private SchemaInfoFilter GetTableSchemaInfoFilter(DatabaseObject dbObject)
        {
            var filter = GetSchemaInfoFilter(dbObject);

            filter.TableNames = new[] { dbObject.Name };

            return filter;
        }

        private SchemaInfoFilter GetViewSchemaInfoFilter(DatabaseObject dbObject)
        {
            var filter = GetSchemaInfoFilter(dbObject);

            filter.ViewNames = new[] { dbObject.Name };

            return filter;
        }

        private SchemaInfoFilter GetDbObjectSchemaInfoFilter(DatabaseObject dbObject)
        {
            var filter = GetSchemaInfoFilter(dbObject);

            if (dbObject is Table)
                filter.TableNames = new[] { dbObject.Name };
            else if (dbObject is View)
                filter.ViewNames = new[] { dbObject.Name };
            else if (dbObject is Function)
                filter.FunctionNames = new[] { dbObject.Name };
            else if (dbObject is Procedure) filter.ProcedureNames = new[] { dbObject.Name };

            return filter;
        }
    }
}