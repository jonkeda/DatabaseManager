//using DatabaseInterpreter.Geometry;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using Microsoft.SqlServer.Types;
using MySqlConnector;
using NpgsqlTypes;
using PgGeom = NetTopologySuite.Geometries;

namespace DatabaseInterpreter.Core
{
    public abstract class DbScriptGenerator
    {
        protected DatabaseType databaseType;
        protected DbInterpreter dbInterpreter;
        protected DbInterpreterOption option;
        protected string scriptsDelimiter;

        public DbScriptGenerator(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
            option = dbInterpreter.Option;
            databaseType = dbInterpreter.DatabaseType;
            scriptsDelimiter = dbInterpreter.ScriptsDelimiter;
        }

        #region Schema Scripts

        public abstract ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo);

        protected virtual List<Script> GenerateScriptDbObjectScripts<T>(List<T> dbObjects)
            where T : ScriptDbObject
        {
            var scripts = new List<Script>();

            foreach (var dbObject in dbObjects)
            {
                dbInterpreter.FeedbackInfo(OperationState.Begin, dbObject);

                var hasNewLine = scriptsDelimiter.Contains(Environment.NewLine);

                var definition = dbObject.Definition.Trim();

                scripts.Add(new CreateDbObjectScript<T>(definition));

                if (!hasNewLine)
                {
                    if (!definition.EndsWith(scriptsDelimiter)) scripts.Add(new SpliterScript(scriptsDelimiter));
                }
                else
                {
                    scripts.Add(new NewLineSript());
                    scripts.Add(new SpliterScript(scriptsDelimiter));
                }

                scripts.Add(new NewLineSript());

                dbInterpreter.FeedbackInfo(OperationState.End, dbObject);
            }

            return scripts;
        }

        #endregion

        #region Data Scripts

        public virtual async Task<string> GenerateDataScriptsAsync(SchemaInfo schemaInfo)
        {
            var sb = new StringBuilder();

            if (option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                ClearScriptFile(GenerateScriptMode.Data);

            using (var connection = dbInterpreter.CreateConnection())
            {
                var tableCount = schemaInfo.Tables.Count;
                var count = 0;

                foreach (var table in schemaInfo.Tables)
                {
                    if (dbInterpreter.CancelRequested) break;

                    count++;

                    var strTableCount = $"({count}/{tableCount})";
                    var tableName = table.Name;

                    var columns = schemaInfo.TableColumns
                        .Where(item => item.Schema == table.Schema && item.TableName == tableName)
                        .OrderBy(item => item.Order).ToList();

                    var isSelfReference = TableReferenceHelper.IsSelfReference(tableName, schemaInfo.TableForeignKeys);

                    var primaryKey = schemaInfo.TablePrimaryKeys.FirstOrDefault(item =>
                        item.Schema == table.Schema && item.TableName == tableName);

                    var primaryKeyColumns = primaryKey == null
                        ? ""
                        : string.Join(",",
                            primaryKey.Columns.OrderBy(item => item.Order)
                                .Select(item => GetQuotedString(item.ColumnName)));

                    var total = await dbInterpreter.GetTableRecordCountAsync(connection, table);

                    if (option.DataGenerateThreshold.HasValue && total > option.DataGenerateThreshold.Value)
                    {
                        FeedbackInfo(
                            $"Record count of table \"{GetQuotedFullTableName(table)}\" exceeds {option.DataGenerateThreshold.Value},ignore it.");
                        continue;
                    }

                    var pageSize = dbInterpreter.DataBatchSize;

                    FeedbackInfo($"{strTableCount}Table \"{GetQuotedFullTableName(table)}\":record count is {total}.");

                    Dictionary<long, List<Dictionary<string, object>>> dictPagedData;

                    if (isSelfReference)
                    {
                        var fk = schemaInfo.TableForeignKeys.FirstOrDefault(item =>
                            item.Schema == table.Schema
                            && item.TableName == tableName
                            && item.ReferencedTableName == tableName);

                        var fkc = fk.Columns.FirstOrDefault();

                        var referencedColumnName = GetQuotedString(fkc.ReferencedColumnName);
                        var columnName = GetQuotedString(fkc.ColumnName);

                        var strWhere = $" WHERE ({columnName} IS NULL OR {columnName}={referencedColumnName})";

                        dictPagedData = await GetSortedPageData(connection, table, primaryKeyColumns,
                            fkc.ReferencedColumnName, fkc.ColumnName, columns, total, strWhere);
                    }
                    else
                    {
                        dictPagedData = await dbInterpreter.GetPagedDataListAsync(connection, table, columns,
                            primaryKeyColumns, total, total, pageSize);
                    }

                    FeedbackInfo($"{strTableCount}Table \"{GetQuotedFullTableName(table)}\":data read finished.");

                    var scriptOutputMode = option.ScriptOutputMode;

                    if (count > 1)
                    {
                        if (scriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToString)) sb.AppendLine();

                        if (scriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
                            AppendScriptsToFile(Environment.NewLine, GenerateScriptMode.Data);
                    }

                    if (option.BulkCopy && dbInterpreter.SupportBulkCopy &&
                        !scriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile)) continue;

                    if (scriptOutputMode != GenerateScriptOutputMode.None)
                        AppendDataScripts(sb, table, columns, dictPagedData);
                }
            }

            var dataScripts = string.Empty;

            try
            {
                dataScripts = sb.ToString();
            }
            catch (OutOfMemoryException ex)
            {
                FeedbackError("Exception occurs:" + ex.Message);
            }
            finally
            {
                sb.Clear();
            }

            return dataScripts;
        }

        private async Task<Dictionary<long, List<Dictionary<string, object>>>> GetSortedPageData(
            DbConnection connection, Table table, string primaryKeyColumns, string referencedColumnName,
            string fkColumnName, List<TableColumn> columns, long total, string whereClause = "")
        {
            var quotedTableName = GetQuotedDbObjectNameWithSchema(table);

            var pageSize = dbInterpreter.DataBatchSize;

            var batchCount = Convert.ToInt64(await dbInterpreter.GetScalarAsync(connection,
                $"SELECT COUNT(1) FROM {quotedTableName} {whereClause}"));

            var dictPagedData = await dbInterpreter.GetPagedDataListAsync(connection, table, columns, primaryKeyColumns,
                total, batchCount, pageSize, whereClause);

            var parentValues = dictPagedData.Values.SelectMany(item => item.Select(t =>
                t[primaryKeyColumns.Trim(dbInterpreter.QuotationLeftChar, dbInterpreter.QuotationRightChar)])).ToList();

            if (parentValues.Count > 0)
            {
                var parentColumn =
                    columns.FirstOrDefault(item => item.Schema == table.Schema && item.Name == fkColumnName);

                var parentValuesPageCount =
                    PaginationHelper.GetPageCount(parentValues.Count, option.InQueryItemLimitCount);

                for (long parentValuePageNumber = 1;
                     parentValuePageNumber <= parentValuesPageCount;
                     parentValuePageNumber++)
                {
                    var pagedParentValues = parentValues.Skip((int)(parentValuePageNumber - 1) * pageSize)
                        .Take(option.InQueryItemLimitCount);

                    var parsedValues = pagedParentValues.Select(item => ParseValue(parentColumn, item, true));

                    var inCondition = GetWhereInCondition(parsedValues, fkColumnName);

                    whereClause = $@" WHERE ({inCondition})
                                      AND ({GetQuotedString(fkColumnName)}<>{GetQuotedString(referencedColumnName)})";

                    batchCount = Convert.ToInt64(await dbInterpreter.GetScalarAsync(connection,
                        $"SELECT COUNT(1) FROM {quotedTableName} {whereClause}"));

                    if (batchCount > 0)
                    {
                        var dictChildPagedData = await GetSortedPageData(connection, table, primaryKeyColumns,
                            referencedColumnName, fkColumnName, columns, total, whereClause);

                        foreach (var kp in dictChildPagedData)
                        {
                            var pageNumber = dictPagedData.Keys.Max(item => item);
                            dictPagedData.Add(pageNumber + 1, kp.Value);
                        }
                    }
                }
            }

            return dictPagedData;
        }

        private string GetWhereInCondition(IEnumerable<object> values, string columnName)
        {
            var valuesCount = values.Count();
            var sb = new StringBuilder();

            var oracleLimitCount = 1000; //oracle where in items count is limit to 1000

            if (valuesCount > oracleLimitCount && databaseType == DatabaseType.Oracle)
            {
                var groups = values.Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / oracleLimitCount).Select(x => x.Select(v => v.Value));

                var count = 0;

                foreach (var gp in groups)
                {
                    sb.AppendLine(
                        $"{(count > 0 ? " OR " : "")}{GetQuotedString(columnName)} IN ({string.Join(",", gp)})");

                    count++;
                }
            }
            else
            {
                sb.Append($"{GetQuotedString(columnName)} IN ({string.Join(",", values)})");
            }

            return sb.ToString();
        }

        public virtual Dictionary<string, object> AppendDataScripts(StringBuilder sb, Table table,
            IEnumerable<TableColumn> columns, Dictionary<long, List<Dictionary<string, object>>> dictPagedData)
        {
            var parameters = new Dictionary<string, object>();

            var appendString = option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToString);
            var appendFile = option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile);

            var excludeColumnNames = new List<string>();

            var excludeIdentityColumn = false;

            if (databaseType == DatabaseType.Oracle && dbInterpreter.IsLowDbVersion())
            {
                excludeIdentityColumn = false;
            }
            else
            {
                if (option.TableScriptsGenerateOption.GenerateIdentity) excludeIdentityColumn = true;
            }

            excludeColumnNames.AddRange(columns.Where(item => item.IsIdentity && excludeIdentityColumn)
                .Select(item => item.Name));

            if (option.ExcludeGeometryForData)
                excludeColumnNames.AddRange(columns.Where(item => DataTypeHelper.IsGeometryType(item.DataType))
                    .Select(item => item.Name));

            excludeColumnNames.AddRange(columns.Where(item => item.IsComputed).Select(item => item.Name));

            var identityColumnHasBeenExcluded =
                excludeColumnNames.Any(item => columns.Any(col => col.Name == item && col.IsIdentity));
            var computeColumnHasBeenExcluded = columns.Any(item => item.IsComputed);

            var canBatchInsert = true;

            if (databaseType == DatabaseType.Oracle)
                if (identityColumnHasBeenExcluded)
                    canBatchInsert = false;

            foreach (var kp in dictPagedData)
            {
                if (kp.Value.Count == 0) continue;

                var sbFilePage = new StringBuilder();

                var tableName = GetQuotedFullTableName(table);
                var columnNames = GetQuotedColumnNames(columns.Where(item => !excludeColumnNames.Contains(item.Name)));
                var insert = !canBatchInsert
                    ? ""
                    : $"{GetBatchInsertPrefix()} {tableName}({GetQuotedColumnNames(columns.Where(item => !excludeColumnNames.Contains(item.Name)))})VALUES";

                if (appendString)
                {
                    if (kp.Key > 1) sb.AppendLine();

                    if (!string.IsNullOrEmpty(insert)) sb.AppendLine(insert);
                }

                if (appendFile)
                {
                    if (kp.Key > 1) sbFilePage.AppendLine();

                    if (!string.IsNullOrEmpty(insert)) sbFilePage.AppendLine(insert);
                }

                var rowCount = 0;

                foreach (var row in kp.Value)
                {
                    rowCount++;

                    var rowValues = GetRowValues(row, rowCount - 1, columns, excludeColumnNames, kp.Key, false,
                        out var insertParameters);

                    var values = $"({string.Join(",", rowValues.Select(item => item == null ? "NULL" : item))})";

                    if (insertParameters != null)
                        foreach (var para in insertParameters)
                            parameters.Add(para.Key, para.Value);

                    var isAllEnd = rowCount == kp.Value.Count;

                    var beginChar = canBatchInsert
                        ? GetBatchInsertItemBefore(tableName,
                            identityColumnHasBeenExcluded || computeColumnHasBeenExcluded ? columnNames : "",
                            rowCount == 1)
                        : $"INSERT INTO {tableName}({columnNames}) VALUES";
                    var endChar = canBatchInsert ? GetBatchInsertItemEnd(isAllEnd) :
                        isAllEnd ? ";" :
                        canBatchInsert ? "," : ";";

                    values = $"{beginChar}{values}{endChar}";

                    if (option.RemoveEmoji) values = StringHelper.RemoveEmoji(values);

                    if (appendString) sb.AppendLine(values);

                    if (appendFile)
                    {
                        var fileRowValues = GetRowValues(row, rowCount - 1, columns, excludeColumnNames, kp.Key, true,
                            out var _);
                        var fileValues =
                            $"({string.Join(",", fileRowValues.Select(item => item == null ? "NULL" : item))})";

                        sbFilePage.AppendLine($"{beginChar}{fileValues}{endChar}");
                    }
                }

                if (appendFile) AppendScriptsToFile(sbFilePage.ToString(), GenerateScriptMode.Data);
            }

            return parameters;
        }

        protected virtual string GetBatchInsertPrefix()
        {
            return "INSERT INTO";
        }

        protected virtual string GetBatchInsertItemBefore(string tableName, string columnNames, bool isFirstRow)
        {
            return "";
        }

        protected virtual string GetBatchInsertItemEnd(bool isAllEnd)
        {
            return isAllEnd ? ";" : ",";
        }

        private List<object> GetRowValues(Dictionary<string, object> row, int rowIndex,
            IEnumerable<TableColumn> columns, List<string> excludeColumnNames, long pageNumber, bool isAppendToFile,
            out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();

            var values = new List<object>();

            foreach (var column in columns)
            {
                var columnName = column.Name;

                if (!row.ContainsKey(columnName)) continue;

                if (!excludeColumnNames.Contains(column.Name))
                {
                    var value = row[columnName];
                    var parsedValue = ParseValue(column, value);
                    var isBitArray = row[column.Name]?.GetType() == typeof(BitArray);
                    var isBytes = ValueHelper.IsBytes(parsedValue) || isBitArray;
                    var isNullValue = value == DBNull.Value || parsedValue?.ToString() == "NULL";

                    if (!isNullValue)
                    {
                        if (!isAppendToFile)
                        {
                            var needInsertParameter = NeedInsertParameter(column, parsedValue);

                            if ((isBytes && !option.TreatBytesAsNullForExecuting) || needInsertParameter)
                            {
                                var parameterName = $"P{pageNumber}_{rowIndex}_{column.Name}";

                                var parameterPlaceholder = dbInterpreter.CommandParameterChar + parameterName;

                                if (databaseType != DatabaseType.Postgres && isBitArray)
                                {
                                    var bitArray = parsedValue as BitArray;
                                    var bytes = new byte[bitArray.Length];
                                    bitArray.CopyTo(bytes, 0);

                                    parsedValue = bytes;
                                }

                                parameters.Add(parameterPlaceholder, parsedValue);

                                parsedValue = parameterPlaceholder;
                            }
                            else if (isBytes && option.TreatBytesAsNullForExecuting)
                            {
                                parsedValue = null;
                            }
                        }
                        else
                        {
                            if (isBytes)
                            {
                                if (option.TreatBytesAsHexStringForFile)
                                    parsedValue = GetBytesConvertHexString(parsedValue, column.DataType);
                                else
                                    parsedValue = null;
                            }
                        }
                    }

                    if (DataTypeHelper.IsUserDefinedType(column))
                    {
                        if (databaseType == DatabaseType.Postgres)
                            parsedValue = $"row({parsedValue})";
                        else if (databaseType == DatabaseType.Oracle)
                            parsedValue = $"{GetQuotedString(column.DataType)}({parsedValue})";
                    }

                    values.Add(parsedValue);
                }
            }

            return values;
        }

        protected virtual bool NeedInsertParameter(TableColumn column, object value)
        {
            return false;
        }

        protected virtual string GetBytesConvertHexString(object value, string dataType)
        {
            return null;
        }

        private object ParseValue(TableColumn column, object value, bool bytesAsString = false)
        {
            if (value != null)
            {
                var type = value.GetType();
                var needQuotated = false;
                var strValue = "";

                if (type == typeof(DBNull)) return "NULL";

                if (value is SqlGeography sgg && sgg.IsNull) return "NULL";

                if (value is SqlGeometry sgm && sgm.IsNull) return "NULL";

                if (type == typeof(byte[]))
                {
                    if (((byte[])value).Length == 16) //GUID
                    {
                        var str = ValueHelper.ConvertGuidBytesToString((byte[])value, databaseType, column.DataType,
                            column.MaxLength, bytesAsString);

                        if (!string.IsNullOrEmpty(str))
                        {
                            needQuotated = true;
                            strValue = str;
                        }
                        else
                        {
                            return value;
                        }
                    }
                    else
                    {
                        return value;
                    }
                }

                var oracleSemicolon = false;
                var dataType = column.DataType.ToLower();

                switch (type.Name)
                {
                    case nameof(Guid):

                        needQuotated = true;
                        if (databaseType == DatabaseType.Oracle && dataType == "raw" && column.MaxLength == 16)
                            strValue = StringHelper.GuidToRaw(value.ToString());
                        else
                            strValue = value.ToString();
                        break;

                    case nameof(String):

                        needQuotated = true;
                        strValue = value.ToString();

                        if (databaseType == DatabaseType.Oracle)
                            if (strValue.Contains(";"))
                                oracleSemicolon = true;
                        /*                            else if (DataTypeHelper.IsGeometryType(dataType))
                            {
                                needQuotated = false;
                                strValue = this.GetOracleGeometryInsertValue(column, value);
                            }*/
                        break;

                    case nameof(DateTime):
                    case nameof(DateTimeOffset):
                    case nameof(MySqlDateTime):

                        if (databaseType == DatabaseType.Oracle)
                        {
                            if (type.Name == nameof(MySqlDateTime))
                            {
                                var dateTime = ((MySqlDateTime)value).GetDateTime();

                                strValue = GetOracleDatetimeConvertString(dateTime);
                            }
                            else if (type.Name == nameof(DateTime))
                            {
                                var dateTime = Convert.ToDateTime(value);

                                strValue = GetOracleDatetimeConvertString(dateTime);
                            }
                            else if (type.Name == nameof(DateTimeOffset))
                            {
                                var dtOffset = DateTimeOffset.Parse(value.ToString());
                                var millisecondLength = dtOffset.Millisecond.ToString().Length;
                                var strMillisecond = millisecondLength == 0
                                    ? ""
                                    : $".{"f".PadLeft(millisecondLength, 'f')}";
                                var format = $"yyyy-MM-dd HH:mm:ss{strMillisecond}";

                                var strDtOffset = dtOffset.ToString(format) +
                                                  $"{dtOffset.Offset.Hours}:{dtOffset.Offset.Minutes}";

                                strValue = $@"TO_TIMESTAMP_TZ('{strDtOffset}','yyyy-MM-dd HH24:MI:ssxff TZH:TZM')";
                            }
                        }
                        else if (databaseType == DatabaseType.MySql)
                        {
                            if (type.Name == nameof(DateTime))
                            {
                                var dt = (DateTime)value;

                                if (dt > MySqlInterpreter.Timestamp_Max_Value.ToLocalTime())
                                    value = MySqlInterpreter.Timestamp_Max_Value.ToLocalTime();
                            }
                            else if (type.Name == nameof(DateTimeOffset))
                            {
                                var dtOffset = DateTimeOffset.Parse(value.ToString());

                                if (dtOffset > MySqlInterpreter.Timestamp_Max_Value.ToLocalTime())
                                    dtOffset = MySqlInterpreter.Timestamp_Max_Value.ToLocalTime();

                                strValue =
                                    $"'{dtOffset.DateTime.Add(dtOffset.Offset).ToString("yyyy-MM-dd HH:mm:ss.ffffff")}'";
                            }
                        }

                        if (string.IsNullOrEmpty(strValue))
                        {
                            needQuotated = true;
                            strValue = value.ToString();
                        }

                        break;

                    case nameof(Boolean):

                        if (databaseType == DatabaseType.Postgres)
                            strValue = value.ToString().ToLower();
                        else
                            strValue = value.ToString() == "True" ? "1" : "0";
                        break;
                    case nameof(TimeSpan):

                        if (databaseType == DatabaseType.Oracle) return value;

                        needQuotated = true;

                        if (dataType.Contains("datetime")
                            || dataType.Contains("timestamp")
                           )
                        {
                            var dateTime =
                                dbInterpreter.MinDateTime.AddSeconds(TimeSpan.Parse(value.ToString()).TotalSeconds);

                            strValue = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            strValue = value.ToString();
                        }

                        break;
                    case nameof(SqlGeography):
                    case nameof(SqlGeometry):
                    case nameof(PgGeom.Point):
                    case nameof(PgGeom.LineString):
                    case nameof(PgGeom.Polygon):
                    case nameof(PgGeom.MultiPoint):
                    case nameof(PgGeom.MultiLineString):
                    case nameof(PgGeom.MultiPolygon):
                    case nameof(PgGeom.GeometryCollection):
                        var srid = 0;

                        if (value is SqlGeography sgg1) srid = sgg1.STSrid.Value;
                        else if (value is SqlGeometry sgm1) srid = sgm1.STSrid.Value;
                        else if (value is PgGeom.Geometry g) srid = g.SRID;
                        /*
                                                if (this.databaseType == DatabaseType.MySql)
                                                {
                                                    strValue = $"ST_GeomFromText('{this.GetCorrectGeometryText(value, dataType)}',{srid})";
                                                }
                                                else if (this.databaseType == DatabaseType.Oracle)
                                                {
                                                    strValue = this.GetOracleGeometryInsertValue(column, value, srid);
                                                }
                        */
                        else
                        {
                            needQuotated = true;
                            strValue = value.ToString();
                        }

                        break;
                    case nameof(SqlHierarchyId):
                    case nameof(NpgsqlLine):
                    case nameof(NpgsqlBox):
                    case nameof(NpgsqlCircle):
                    case nameof(NpgsqlPath):
                    case nameof(NpgsqlLSeg):
                    case nameof(NpgsqlTsVector):
                        needQuotated = true;
                        strValue = value.ToString();

                        break;

                    /*                    case nameof(SdoGeometry):
                                        case nameof(StGeometry):
                                            strValue = this.GetOracleGeometryInsertValue(column, value);

                                            break;
                    */
                    default:
                        if (string.IsNullOrEmpty(strValue)) strValue = value.ToString();
                        break;
                }

                if (needQuotated)
                {
                    strValue = $"{dbInterpreter.UnicodeLeadingFlag}'{ValueHelper.TransferSingleQuotation(strValue)}'";

                    if (oracleSemicolon)
                        strValue = strValue.Replace(";",
                            $"'{dbInterpreter.STR_CONCAT_CHARS}{OracleInterpreter.SEMICOLON_FUNC}{dbInterpreter.STR_CONCAT_CHARS}'");

                    return strValue;
                }

                return strValue;
            }

            return null;
        }

        /*  private string GetOracleGeometryInsertValue(TableColumn column, object value, int? srid = null)
          {
              string str = this.GetCorrectGeometryText(value, column.DataType.ToLower());
  
              string strValue = "";
  
              if (str.Length > 4000) //oracle allow max char length
              {
                  strValue = "NULL";
              }
              else
              {
                  string dataType = column.DataType.ToUpper();
                  string dataTypeSchema = column.DataTypeSchema?.ToUpper();
  
                  string strSrid = srid.HasValue ? $",{srid.Value}" : "";
  
                  if (dataType == "SDO_GEOMETRY")
                  {
                      strValue = $"SDO_GEOMETRY('{str}'{strSrid})";
                  }
                  else if (dataType == "ST_GEOMETRY")
                  {
                      if (string.IsNullOrEmpty(dataTypeSchema) || dataTypeSchema == "MDSYS" || dataTypeSchema == "PUBLIC") //PUBLIC is synonyms of MDSYS
                      {
                          strValue = $"MDSYS.ST_GEOMETRY(SDO_GEOMETRY('{str}'{strSrid}))";
                      }
                      else if (dataTypeSchema == "SDE")
                      {
                          strValue = $"SDE.ST_GEOMETRY('{str}'{strSrid})";
                      }
                  }
              }
  
              return strValue;
          }*/

        private string GetOracleDatetimeConvertString(DateTime dateTime)
        {
            var millisecondLength = dateTime.Millisecond.ToString().Length;
            var strMillisecond = millisecondLength == 0 ? "" : $".{"f".PadLeft(millisecondLength, 'f')}";
            var format = $"yyyy-MM-dd HH:mm:ss{strMillisecond}";

            return $"TO_TIMESTAMP('{dateTime.ToString(format)}','yyyy-MM-dd hh24:mi:ssxff')";
        }

        /* private string GetCorrectGeometryText(object value, string dataType)
         {
             if (value is SqlGeography sg)
             {
                 if (this.databaseType != DatabaseType.SqlServer && dataType != "geography")
                 {
                     return SqlGeographyHelper.ToPostgresGeometry(sg).AsText();
                 }
             }
             else if (value is PgGeom.Geometry pg)
             {
                 if (pg.UserData != null && pg.UserData is PostgresGeometryCustomInfo pgi)
                 {
                     if (pgi.IsGeography)
                     {
                         if (this.databaseType != DatabaseType.Postgres && dataType != "geography")
                         {
                             PostgresGeometryHelper.ReverseCoordinates(pg);
 
                             return pg.ToString();
                         }
                     }
                 }
             }
 
             return value.ToString();
         }*/

        #endregion

        #region Append Scripts

        public string GetScriptOutputFilePath(GenerateScriptMode generateScriptMode)
        {
            var database = dbInterpreter.ConnectionInfo.Database;
            var databaseName = !string.IsNullOrEmpty(database) && File.Exists(database)
                ? Path.GetFileNameWithoutExtension(database)
                : database;

            var fileName =
                $"{databaseName}_{databaseType}_{DateTime.Today.ToString("yyyyMMdd")}_{generateScriptMode.ToString()}.sql";
            var filePath = Path.Combine(option.ScriptOutputFolder, fileName);
            return filePath;
        }

        public virtual void AppendScriptsToFile(string content, GenerateScriptMode generateScriptMode,
            bool overwrite = false)
        {
            if (generateScriptMode == GenerateScriptMode.Schema) content = StringHelper.ToSingleEmptyLine(content);

            var filePath = GetScriptOutputFilePath(generateScriptMode);

            var directoryName = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);

            if (!overwrite)
                File.AppendAllText(filePath, content, Encoding.UTF8);
            else
                File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        public void ClearScriptFile(GenerateScriptMode generateScriptMode)
        {
            var filePath = GetScriptOutputFilePath(generateScriptMode);

            if (File.Exists(filePath)) File.WriteAllText(filePath, "", Encoding.UTF8);
        }

        #endregion

        #region Alter Table

        public abstract Script RenameTable(Table table, string newName);

        public abstract Script SetTableComment(Table table, bool isNew = true);

        public abstract Script AddTableColumn(Table table, TableColumn column);

        public abstract Script RenameTableColumn(Table table, TableColumn column, string newName);

        public abstract Script AlterTableColumn(Table table, TableColumn newColumn, TableColumn oldColumn);

        public abstract Script SetTableColumnComment(Table table, TableColumn column, bool isNew = true);

        public abstract Script DropTableColumn(TableColumn column);

        public abstract Script DropPrimaryKey(TablePrimaryKey primaryKey);

        public abstract Script DropForeignKey(TableForeignKey foreignKey);

        public abstract Script AddPrimaryKey(TablePrimaryKey primaryKey);

        public abstract Script AddForeignKey(TableForeignKey foreignKey);

        public abstract Script AddIndex(TableIndex index);

        public abstract Script DropIndex(TableIndex index);

        public abstract Script AddCheckConstraint(TableConstraint constraint);

        public abstract Script DropCheckConstraint(TableConstraint constraint);

        public abstract Script SetIdentityEnabled(TableColumn column, bool enabled);

        #endregion

        #region Database Operation

        public abstract Script CreateSchema(DatabaseSchema schema);
        public abstract Script CreateUserDefinedType(UserDefinedType userDefinedType);
        public abstract Script CreateSequence(Sequence sequence);

        public abstract ScriptBuilder CreateTable(Table table, IEnumerable<TableColumn> columns,
            TablePrimaryKey primaryKey,
            IEnumerable<TableForeignKey> foreignKeys,
            IEnumerable<TableIndex> indexes,
            IEnumerable<TableConstraint> constraints);

        public abstract Script DropUserDefinedType(UserDefinedType userDefinedType);
        public abstract Script DropSequence(Sequence sequence);
        public abstract Script DropTable(Table table);
        public abstract Script DropView(View view);
        public abstract Script DropTrigger(TableTrigger trigger);
        public abstract Script DropFunction(Function function);
        public abstract Script DropProcedure(Procedure procedure);
        public abstract IEnumerable<Script> SetConstrainsEnabled(bool enabled);

        public virtual Script Create(DatabaseObject dbObject)
        {
            if (dbObject is TableColumn column)
                return AddTableColumn(new Table { Schema = column.Schema, Name = column.TableName }, column);
            if (dbObject is TablePrimaryKey primaryKey)
                return AddPrimaryKey(primaryKey);
            if (dbObject is TableForeignKey foreignKey)
                return AddForeignKey(foreignKey);
            if (dbObject is TableIndex index)
                return AddIndex(index);
            if (dbObject is TableConstraint constraint)
                return AddCheckConstraint(constraint);
            if (dbObject is UserDefinedType userDefinedType)
                return CreateUserDefinedType(userDefinedType);
            if (dbObject is ScriptDbObject scriptDbObject)
                return new CreateDbObjectScript<ScriptDbObject>(scriptDbObject.Definition);

            throw new NotSupportedException($"Not support to add {dbObject.GetType().Name} using this method.");
        }

        public virtual Script Drop(DatabaseObject dbObject)
        {
            if (dbObject is TableColumn column)
                return DropTableColumn(column);
            if (dbObject is TablePrimaryKey primaryKey)
                return DropPrimaryKey(primaryKey);
            if (dbObject is TableForeignKey foreignKey)
                return DropForeignKey(foreignKey);
            if (dbObject is TableIndex index)
                return DropIndex(index);
            if (dbObject is TableConstraint constraint)
                return DropCheckConstraint(constraint);
            if (dbObject is TableTrigger trigger)
                return DropTrigger(trigger);
            if (dbObject is View view)
                return DropView(view);
            if (dbObject is Function function)
                return DropFunction(function);
            if (dbObject is Procedure procedure)
                return DropProcedure(procedure);
            if (dbObject is Table table)
                return DropTable(table);
            if (dbObject is UserDefinedType userDefinedType)
                return DropUserDefinedType(userDefinedType);
            if (dbObject is Sequence sequence) return DropSequence(sequence);

            throw new NotSupportedException($"Not support to drop {dbObject.GetType().Name}.");
        }

        #endregion

        #region Common Method

        public string GetQuotedString(string str)
        {
            return dbInterpreter.GetQuotedString(str);
        }

        public string GetQuotedColumnNames(IEnumerable<TableColumn> columns)
        {
            return dbInterpreter.GetQuotedColumnNames(columns);
        }

        public string GetQuotedDbObjectNameWithSchema(DatabaseObject dbObject)
        {
            return dbInterpreter.GetQuotedDbObjectNameWithSchema(dbObject);
        }

        public string GetQuotedDbObjectNameWithSchema(string schema, string dbObjName)
        {
            return dbInterpreter.GetQuotedDbObjectNameWithSchema(schema, dbObjName);
        }

        public string GetQuotedFullTableName(Table table)
        {
            return GetQuotedDbObjectNameWithSchema(table);
        }

        public string GetQuotedFullTableName(TableChild tableChild)
        {
            if (string.IsNullOrEmpty(tableChild.Schema))
                return GetQuotedString(tableChild.TableName);
            return $"{GetQuotedString(tableChild.Schema)}.{GetQuotedString(tableChild.TableName)}";
        }

        public string GetQuotedFullTableChildName(TableChild tableChild)
        {
            var fullTableName = GetQuotedFullTableName(tableChild);
            return $"{fullTableName}.{GetQuotedString(tableChild.Name)}";
        }

        public string TransferSingleQuotationString(string comment)
        {
            if (string.IsNullOrEmpty(comment)) return comment;

            return ValueHelper.TransferSingleQuotation(comment);
        }

        protected string GetCreateTableOption()
        {
            var option = CreateTableOptionManager.GetCreateTableOption(databaseType);

            if (option == null) return string.Empty;

            var sb = new StringBuilder();

            void AppendValue(string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (sb.Length > 0) sb.AppendLine();

                    sb.Append(value);
                }
            }

            foreach (var item in option.Items)
                if (!string.IsNullOrEmpty(item))
                {
                    var items = item.Split(CreateTableOptionManager.OptionValueItemsSeperator);

                    foreach (var subItem in items) AppendValue(subItem);
                }

            return sb.ToString();
        }

        #endregion

        #region Feedback

        public void FeedbackInfo(OperationState state, DatabaseObject dbObject)
        {
            dbInterpreter.FeedbackInfo(state, dbObject);
        }

        public void FeedbackInfo(string message)
        {
            dbInterpreter.FeedbackInfo(message);
        }

        public void FeedbackError(string message, bool skipError = false)
        {
            dbInterpreter.FeedbackError(message, skipError);
        }

        #endregion
    }
}