using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class DbStatistic
    {
        protected ConnectionInfo connectionInfo;
        protected DatabaseType databaseType;

        public FeedbackHandler OnFeedback;

        public DbStatistic(DatabaseType databaseType, ConnectionInfo connectionInfo)
        {
            this.databaseType = databaseType;
            this.connectionInfo = connectionInfo;
        }

        public virtual async Task<IEnumerable<TableRecordCount>> CountTableRecords()
        {
            var results = Enumerable.Empty<TableRecordCount>();

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(databaseType, connectionInfo,
                new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple });

            using (var connection = dbInterpreter.CreateConnection())
            {
                Feedback("Begin to get tables...");

                var tables = await dbInterpreter.GetTablesAsync(connection);

                Feedback($"Got {tables.Count} {(tables.Count > 1 ? "tables" : "table")}.");

                var sb = new SqlBuilder();

                var i = 0;

                foreach (var table in tables)
                {
                    if (i > 0 && i < tables.Count) sb.Append("UNION ALL");

                    var tableName = dbInterpreter.GetQuotedDbObjectNameWithSchema(table);

                    sb.Append(
                        $"SELECT '{tableName}' AS {dbInterpreter.GetQuotedString("TableName")}, COUNT(1) AS {dbInterpreter.GetQuotedString("RecordCount")} FROM {tableName}");

                    i++;
                }

                Feedback("Begin to read records count...");

                results = await connection.QueryAsync<TableRecordCount>(sb.Content);

                Feedback("End read records count.");
            }

            return results;
        }

        protected void Feedback(string message)
        {
            OnFeedback?.Invoke(new FeedbackInfo { InfoType = FeedbackInfoType.Info, Message = message, Owner = this });
        }
    }
}