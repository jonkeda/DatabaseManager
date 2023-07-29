using Database.Tests.Helpers;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;

namespace Database.Tests.Translator;

public abstract class TranslatorTest
{
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