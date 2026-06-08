using SalesApp.Libs;
using Xunit;

namespace SalesApp.Tests.Libs
{
    public class CotaDecomposerTests
    {
        [Theory]
        [InlineData("012173;4103;0;MARIO;1100326334", "12173", "4103", "MARIO", "1100326334")]
        [InlineData("012176;2578;0;TIAGO DA SILVA;1100326702", "12176", "2578", "TIAGO DA SILVA", "1100326702")]
        [InlineData(" 012176 ; 2578 ; 0 ; TIAGO DA SILVA ; 1100326702 ", "12176", "2578", "TIAGO DA SILVA", "1100326702")]
        public void Decompose_ShouldParseValidConcatenatedString(string input, string expectedGroup, string expectedMatricula, string expectedCustomer, string expectedContract)
        {
            // Act
            var result = CotaDecomposer.Decompose(input);

            // Assert
            Assert.True(result.IsFromConcatenatedString);
            Assert.Equal(expectedGroup, result.Group);
            Assert.Equal(expectedMatricula, result.Matricula);
            Assert.Equal(expectedCustomer, result.Customer);
            Assert.Equal(expectedContract, result.Contract);
        }

        [Fact]
        public void Decompose_ShouldHandleStandardContractNumber()
        {
            // Arrange
            var input = "1100326334";

            // Act
            var result = CotaDecomposer.Decompose(input);

            // Assert
            Assert.False(result.IsFromConcatenatedString);
            Assert.Null(result.Group);
            Assert.Null(result.Matricula);
            Assert.Null(result.Customer);
            Assert.Equal("1100326334", result.Contract);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Decompose_ShouldHandleEmptyInput(string? input)
        {
            // Act
            var result = CotaDecomposer.Decompose(input);

            // Assert
            Assert.False(result.IsFromConcatenatedString);
            Assert.Null(result.Contract);
        }

        [Fact]
        public void Decompose_ShouldHandleIncompleteConcatenatedString()
        {
            // Arrange
            var input = "012173;4103;0";

            // Act
            var result = CotaDecomposer.Decompose(input);

            // Assert
            Assert.True(result.IsFromConcatenatedString);
            Assert.Null(result.Group);
            Assert.Null(result.Matricula);
            Assert.Null(result.Customer);
            Assert.Equal("0", result.Contract); // Takes the last part
        }
    }
}
