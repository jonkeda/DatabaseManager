/*using System;
using DatabaseInterpreter.Model;
using Xunit;
// using Moq;

namespace DatabaseInterpreter.Utility.Tests
{
    public class ValueHelperTests
    {
        private readonly byte[] _guidBytes = new byte[] {
            0x78, 0x05, 0x4d, 0xfa, 0x25, 0x49, 0xd4, 0x8b,
            0x84, 0xeb, 0x58, 0xc7, 0x06, 0x35, 0x9a, 0x44
        };

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_Null_Value()
        {
            // Arrange
            byte[] value = null;
            DatabaseType databaseType = DatabaseType.SqlServer;
            string dataType = "UniqueIdentifier";
            long? length = null;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(value, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_Value_Length_Not_Equal_16()
        {
            // Arrange
            byte[] value = new byte[] { 1, 2, 3 };
            DatabaseType databaseType = DatabaseType.SqlServer;
            string dataType = "UniqueIdentifier";
            long? length = null;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(value, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Guid_As_String_For_SqlServer_UniqueIdentifier()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.SqlServer;
            string dataType = "UniqueIdentifier";
            long? length = null;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Equal("fa4d0578-4925-8bd4-84eb-58c706359a44", actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Guid_As_String_For_MySql()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.MySql;
            string dataType = "Char";
            long? length = 36;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Equal("fa4d0578-4925-8bd4-84eb-58c706359a44", actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Guid_As_Raw_String_For_Oracle_Raw()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.Oracle;
            string dataType = "Raw";
            long? length = 16;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Equal("FA4D05784925D48B84EB58C706359A44", actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_Bytes_As_String_Is_False()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.Oracle;
            string dataType = "Raw";
            long? length = 16;
            bool bytesAsString = false;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_DatabaseType_Is_Null()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.Unknown;
            string dataType = "Raw";
            long? length = 16;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_DataType_Is_Null()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.Oracle;
            string dataType = null;
            long? length = 16;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }

        [Fact]
        public void ConvertGuidBytesToString_Should_Return_Null_When_Length_Is_Zero()
        {
            // Arrange
            DatabaseType databaseType = DatabaseType.Oracle;
            string dataType = "Raw";
            long? length = 0;
            bool bytesAsString = true;

            // Act
            var actualResult = ValueHelper.ConvertGuidBytesToString(_guidBytes, databaseType, dataType, length, bytesAsString);

            // Assert
            Assert.Null(actualResult);
        }
    }
}
*/