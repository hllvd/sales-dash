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
        // Contract number with no leading zeros — must be preserved exactly as-is
        [InlineData("001696;318;0;GABRIEL FERREIRA ALVES ;1100239686", "1696", "318", "GABRIEL FERREIRA ALVES", "1100239686")]
        // Contract number with a single leading zero — only that zero is stripped (0239686 → 239686)
        [InlineData("001696;318;0;GABRIEL FERREIRA ALVES ;0239686", "1696", "318", "GABRIEL FERREIRA ALVES", "239686")]
        // Contract number starting with 10... — the leading '1' must NOT be stripped
        [InlineData("001696;318;0;GABRIEL FERREIRA ALVES ;10239686", "1696", "318", "GABRIEL FERREIRA ALVES", "10239686")]
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

        [Theory]
        // Standard 10-digit contract number — no leading zeros, must be stored exactly
        [InlineData("1100326334", "1100326334")]
        // 10-digit contract starting with 11 — must NOT be truncated to the trailing digits
        [InlineData("1100239686", "1100239686")]
        // Contract starting with 10 — the leading '1' is not a zero, must be preserved
        [InlineData("10239686", "10239686")]
        // Contract with a single leading zero — only that zero is stripped
        [InlineData("0239686", "239686")]
        // Pure numeric quota-style ID — stored as-is
        [InlineData("239686", "239686")]
        public void Decompose_ShouldHandleStandardContractNumber(string input, string expectedContract)
        {
            // Act
            var result = CotaDecomposer.Decompose(input);

            // Assert
            Assert.False(result.IsFromConcatenatedString);
            Assert.Null(result.Group);
            Assert.Null(result.Matricula);
            Assert.Null(result.Customer);
            Assert.Equal(expectedContract, result.Contract);
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
