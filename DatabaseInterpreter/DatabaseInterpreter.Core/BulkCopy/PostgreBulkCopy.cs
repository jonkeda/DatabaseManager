using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using Npgsql;
using NpgsqlTypes;

namespace DatabaseInterpreter.Core
{
    public class PostgreBulkCopy : IDisposable
    {
        private static readonly Dictionary<string, NpgsqlDbType> dataTypeMappings = new Dictionary<string, NpgsqlDbType>
        {
            { "smallint", NpgsqlDbType.Smallint },
            { "integer", NpgsqlDbType.Integer },
            { "bigint", NpgsqlDbType.Bigint },
            { "real", NpgsqlDbType.Real },
            { "double precision", NpgsqlDbType.Double },
            { "numeric", NpgsqlDbType.Numeric },
            { "money", NpgsqlDbType.Money },
            { "date", NpgsqlDbType.Date },
            { "text", NpgsqlDbType.Text },
            { "character varying", NpgsqlDbType.Varchar }
        };

        private NpgsqlConnection _connection;

        private string _destinationTableName;

        private readonly bool _ownsTheConnection;

        public PostgreBulkCopy(string connectionString) : this(new NpgsqlConnection(connectionString))
        {
            _ownsTheConnection = true;
        }

        public PostgreBulkCopy(NpgsqlConnection connection) : this(connection, null)
        {
        }

        public PostgreBulkCopy(NpgsqlConnection connection, NpgsqlTransaction transation = null)
        {
            _connection = connection;
            _externalTransaction = transation;
        }

        private NpgsqlTransaction _externalTransaction { get; }

        public string DestinationTableName
        {
            get => _destinationTableName;
            set
            {
                if (value == null || value.Length == 0)
                    throw new ArgumentException("Destination table name cannot be null or empty string");

                _destinationTableName = value;
            }
        }

        public bool ColumnNameNeedQuoted { get; set; }
        public bool DetectDateTimeTypeByValues { get; set; }
        public int BulkCopyTimeout { get; set; }

        public IEnumerable<TableColumn> TableColumns { get; set; }

        public void Dispose()
        {
            if (_connection != null)
            {
                if (_ownsTheConnection) _connection.Dispose();

                _connection = null;
            }
        }

        private void ValidateConnection()
        {
            if (_connection == null) throw new Exception("Postgres Database Connection is required");

            if (_externalTransaction != null && _externalTransaction.Connection != _connection)
                throw new Exception("Postgres Transaction mismatch with Oracle Database Connection");
        }

        private async Task OpenConnectionAsync()
        {
            if (_ownsTheConnection && _connection.State != ConnectionState.Open) await _connection.OpenAsync();
        }

        public async Task<ulong> WriteToServerAsync(DataTable table)
        {
            if (table == null) throw new ArgumentNullException("table");

            return await CopyData(table);
        }

        private async Task<ulong> CopyData(DataTable table)
        {
            NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();

            var columnList = GetColumnList(table);

            ValidateConnection();
            await OpenConnectionAsync();

            _connection.TypeMapper.UseNetTopologySuite();

            var commandText = $"COPY {DestinationTableName}({columnList}) FROM STDIN (FORMAT BINARY)";

            using (var writer = _connection.BeginBinaryImport(commandText))
            {
                writer.Timeout = TimeSpan.FromSeconds(BulkCopyTimeout);

                foreach (DataRow row in table.Rows)
                {
                    await writer.StartRowAsync();

                    foreach (DataColumn col in table.Columns)
                    {
                        var result = ParseDbTypeFromDotnetType(col.ColumnName, row[col.ColumnName], col.DataType);

                        await writer.WriteAsync(result.Value, result.Type);
                    }
                }

                return await writer.CompleteAsync();
            }
        }

        private string GetColumnList(DataTable data)
        {
            var columnNames = data.Columns.Cast<DataColumn>()
                .Select(x => GetColumnName(x.ColumnName)).ToArray();

            var columnList = string.Join(",", columnNames);
            return columnList;
        }

        private string GetColumnName(string columnName)
        {
            if (ColumnNameNeedQuoted || columnName.Contains(" "))
                return $@"""{columnName}""";
            return columnName;
        }

        private string GetValueList(DataTable data)
        {
            const string Delimiter = ", ";

            var sb = new StringBuilder();
            for (var i = 1; i <= data.Columns.Count; i++)
            {
                sb.Append(string.Format(":{0}", i));
                sb.Append(Delimiter);
            }

            sb.Length -= Delimiter.Length;

            var valueList = sb.ToString();
            return valueList;
        }

        public void Close()
        {
            Dispose();
        }

        public (dynamic Value, NpgsqlDbType Type) ParseDbTypeFromDotnetType(string columnName, dynamic value, Type t)
        {
            var dbType = NpgsqlDbType.Unknown;

            if (t == typeof(Guid))
            {
                dbType = NpgsqlDbType.Varchar;
            }
            else if (t == typeof(byte[]))
            {
                dbType = NpgsqlDbType.Bytea;
            }
            else if (t == typeof(string))
            {
                dbType = NpgsqlDbType.Text;
            }
            else if (t == typeof(DateTimeOffset))
            {
                dbType = NpgsqlDbType.TimestampTz;
            }
            else if (t == typeof(int))
            {
                dbType = NpgsqlDbType.Integer;
            }
            else if (t == typeof(sbyte))
            {
                dbType = NpgsqlDbType.Smallint;
            }
            else if (t == typeof(byte))
            {
                dbType = NpgsqlDbType.Smallint;
            }
            else if (t == typeof(TimeSpan))
            {
                dbType = NpgsqlDbType.Time;
            }
            else if (t == typeof(bool))
            {
                dbType = NpgsqlDbType.Boolean;
            }
            else
            {
                var targetColumnDataType = FindTableColumnType(columnName)?.ToLower();

                if (t == typeof(short))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Smallint;
                }

                if (t == typeof(long))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Bigint;
                }
                else if (t == typeof(float))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Real;
                }
                else if (t == typeof(double))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Double;
                }
                else if (t == typeof(decimal))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Numeric;
                }
                else if (t == typeof(DateTime))
                {
                    var mappedDataType = GetMappedDataType(targetColumnDataType);

                    if (mappedDataType.HasValue)
                        dbType = mappedDataType.Value;
                    else
                        dbType = NpgsqlDbType.Timestamp;
                }
                else if (Enum.TryParse(t.Name, out NpgsqlDbType _))
                {
                    dbType = (NpgsqlDbType)Enum.Parse(typeof(NpgsqlDbType), t.Name);
                }
            }

            if (dbType == NpgsqlDbType.Unknown) dbType = NpgsqlDbType.Varchar;

            return (value, dbType);
        }

        private string FindTableColumnType(string columnName)
        {
            if (TableColumns != null) return TableColumns.FirstOrDefault(item => item.Name == columnName)?.DataType;

            return null;
        }

        private NpgsqlDbType? GetMappedDataType(string dataType)
        {
            if (dataType != null && dataTypeMappings.ContainsKey(dataType)) return dataTypeMappings[dataType];

            return default;
        }
    }
}