namespace Databases.Tests.Translator.Select
{
    public class SelectTests : TranslatorTest
    {
        [Fact]
        public void Translate1()
        {
            TranslateTSqlToPostgreSql("SelectTest1");
        }
    }
}
