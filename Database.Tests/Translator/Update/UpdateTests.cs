namespace Databases.Tests.Translator.Update
{
    public class UpdateTests : TranslatorTest
    {
        [Fact]
        public void Translate1()
        {
            TranslateTSqlToPostgreSql("UpdateTest1");
        }
    }
}
