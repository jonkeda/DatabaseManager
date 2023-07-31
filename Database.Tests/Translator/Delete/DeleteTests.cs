namespace Databases.Tests.Translator.Delete
{
    public class DeleteTests : TranslatorTest
    {
        [Fact]
        public void Translate1()
        {
            TranslateTSqlToPostgreSql("DeleteTest1");
        }
    }
}
