using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Database.Tests.Translator
{
    public class MsqlSqlTranslations
    {
        [Fact]
        public void TranslateMsSqlToPostgres()
        {
            var sourceScript = "SELECT * FROM TABLE1";

            var sourceDbType = DatabaseType.SqlServer;
            var targetDbType = DatabaseType.Postgres;

            var translateManager = new TranslateManager();

            var result = translateManager.Translate(sourceDbType, targetDbType, sourceScript);

            var resultData = result.Data?.ToString();

        }
    }


}
