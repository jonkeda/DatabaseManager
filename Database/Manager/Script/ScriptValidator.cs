using DatabaseConverter.Core;
using DatabaseInterpreter.Model;
using Databases.SqlAnalyser.Model;
using SqlAnalyser.Model;

namespace DatabaseManager.Core
{
    public class ScriptValidator
    {
        public static SqlSyntaxError ValidateSyntax(DatabaseType databaseType, string script)
        {
            var sqlAnalyser = TranslateHelper.GetSqlAnalyser(databaseType, script);

            var sqlSyntaxError = sqlAnalyser.Validate();

            return sqlSyntaxError;
        }
    }
}