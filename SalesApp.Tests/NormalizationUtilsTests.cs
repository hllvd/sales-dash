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
