using Databases.Converter.Helper;
using Databases.Model.Enum;
using Databases.SqlAnalyser.Model;

namespace Databases.Manager.Script
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