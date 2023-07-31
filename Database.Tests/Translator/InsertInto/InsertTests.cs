namespace Databases.Tests.Translator.InsertInto
{
    public class InsertTests : TranslatorTest
    {
        [Fact]
        public void Translate1()
        {
            TranslateTSqlToPostgreSql("InsertTest1");
        }
    }
}
