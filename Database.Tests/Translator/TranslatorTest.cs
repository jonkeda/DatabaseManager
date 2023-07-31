using Databases.Handlers.TSql;
using Databases.Handlers;
using Databases.Handlers.MySql;
using Databases.Handlers.PlSql;
using Databases.Handlers.PostgreSql;
using Databases.Handlers.Sqlite;
using Databases.Manager.Manager;
using Databases.Model.Enum;
using Databases.Tests.Helpers;

namespace Databases.Tests.Translator;

public abstract class TranslatorTest
{
    static TranslatorTest()
    {
        SqlHandler.RegisterHandler(new TSqlHandler());
        SqlHandler.RegisterHandler(new PlSqlHandler());
        SqlHandler.RegisterHandler(new MySqlHandler());
        SqlHandler.RegisterHandler(new SqliteHandler());
        SqlHandler.RegisterHandler(new PostgreSqlHandler());
    }

    protected void TranslateTSqlToPostgreSql(string name)
    {
        string namespaceName = GetType().Namespace!;
        Translate($"{namespaceName}.{name}.TSql.sql",
            $"{namespaceName}.{name}.PostgreSql.sql");

    }

    protected void Translate(string inFilename, string outFilename)
    {
        var sourceScript = ResourceHelper.GetContent(GetType(), inFilename); ;
        var expectedScript = ResourceHelper.GetContent(GetType(), outFilename); ;

        expectedScript = expectedScript.Replace("\r\n", "\n");

        var translateManager = new TranslateManager();

        var result = translateManager.Translate(DatabaseType.SqlServer,
            DatabaseType.Postgres, sourceScript);

        var resultScript = result.Data.Replace("\r\n", "\n");

        Assert.Equal(expectedScript, resultScript);
    }

}