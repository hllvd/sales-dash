using FluentAssertions;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests
{
    public class ContractTypeExtensionsTests
    {
        [Theory]
        [InlineData(ContractType.Lar, "lar")]
        [InlineData(ContractType.Motores, "motores")]
        public void ToApiString_ValidEnum_ShouldReturnExpectedString(ContractType type, string expected)
        {
            var result = type.ToApiString();
            result.Should().Be(expected);
        }

        [Fact]
        public void ToApiString_InvalidEnum_ShouldReturnNullInsteadOfThrowing()
        {
            var invalidType = (ContractType)1704;
            var result = invalidType.ToApiString();
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(0, "lar")]
        [InlineData(1, "motores")]
        public void ToApiString_NullableInt_Valid_ShouldReturnExpectedString(int input, string expected)
        {
            var result = ContractTypeExtensions.ToApiString(input);
            result.Should().Be(expected);
        }

        [Fact]
        public void ToApiString_NullableInt_Null_ShouldReturnNull()
        {
            var result = ContractTypeExtensions.ToApiString((int?)null);
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(1704)]
        [InlineData(-1)]
        [InlineData(999)]
        public void ToApiString_NullableInt_InvalidValue_ShouldReturnNullInsteadOfThrowing(int invalidValue)
        {
            var result = ContractTypeExtensions.ToApiString(invalidValue);
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("lar", ContractType.Lar)]
        [InlineData("LAR", ContractType.Lar)]
        [InlineData("motores", ContractType.Motores)]
        [InlineData("MOTORES", ContractType.Motores)]
        public void FromApiString_ValidString_ShouldReturnEnum(string input, ContractType expected)
        {
            var result = ContractTypeExtensions.FromApiString(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void FromApiString_EmptyOrNull_ShouldReturnNull(string? input)
        {
            var result = ContractTypeExtensions.FromApiString(input);
            result.Should().BeNull();
        }
    }
}
