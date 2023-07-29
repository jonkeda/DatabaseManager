namespace Database.Tests.Translator
{
    public class SelectTests : TranslatorTest
    {
        [Fact]
        public void TranslateMsSqlToPostgres()
        {
            Translate("Database.Tests.Translator.SelectTests.in.sql",
                "Database.Tests.Translator.SelectTests.out.sql");
        }
    }
}
