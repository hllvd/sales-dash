using Xunit;
using SalesApp.Utils;

namespace SalesApp.Tests.Utils
{
    public class NormalizationUtilsTests
    {
        [Theory]
        [InlineData("012345", "12345")]
        [InlineData("000123", "123")]
        [InlineData("0", "0")]
        [InlineData("000", "0")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("123", "123")]
        [InlineData("A001", "A001")]
        [InlineData("00A001", "A001")]
        // Placeholder cases representing absent/dash data
        [InlineData("-", "")]
        [InlineData("--", "")]
        [InlineData(" - ", "")]
        [InlineData("N/A", "")]
        [InlineData("NA", "")]
        [InlineData("null", "")]
        [InlineData("none", "")]
        [InlineData("undefined", "")]
        [InlineData("sem matricula", "")]
        [InlineData("sem matrícula", "")]
        // Contract number investigation cases
        [InlineData("1100239686", "1100239686")] // no leading zeros — unchanged
        [InlineData("0239686", "239686")]         // one leading zero stripped
        [InlineData("10239686", "10239686")]      // leading '1' is NOT a zero — unchanged
        public void NormalizeNumber_ShouldRemoveLeadingZeros(string input, string expected)
        {
            var result = NormalizationUtils.NormalizeNumber(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("0123", 123)]
        [InlineData("000", 0)]
        [InlineData("ABC", null)]
        [InlineData("", null)]
        public void NormalizeInt_ShouldParseCorrectly(string input, int? expected)
        {
            var result = NormalizationUtils.NormalizeInt(input);
            Assert.Equal(expected, result);
        }
    }
}
