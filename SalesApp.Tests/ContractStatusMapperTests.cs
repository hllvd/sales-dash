using FluentAssertions;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests
{
    public class ContractStatusMapperTests
    {
        [Theory]
        [InlineData("Active", "Active")]
        [InlineData("Normal", "Active")]
        [InlineData("NORMAL", "Active")]
        [InlineData("active", "Active")] // Legacy
        public void MapStatus_ActiveAliases_ShouldMapToActive(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late1", "Late1")]
        [InlineData("NCONT 1 AT", "Late1")]
        [InlineData("ncont 1 at", "Late1")]
        public void MapStatus_Late1Aliases_ShouldMapToLate1(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late2", "Late2")]
        [InlineData("NCONT 2 AT", "Late2")]
        [InlineData("ncont 2 at", "Late2")]
        public void MapStatus_Late2Aliases_ShouldMapToLate2(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late3", "Late3")]
        [InlineData("NCONT 3 AT", "Late3")]
        [InlineData("SUJ. A CANCELAMENTO", "Late3")]
        [InlineData("SUJ. A  CANCELAMENTO", "Late3")] // Double space
        [InlineData("suj. a cancelamento", "Late3")]
        [InlineData("delinquent", "Late3")] // Legacy
        public void MapStatus_Late3Aliases_ShouldMapToLate3(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Defaulted", "Defaulted")]
        [InlineData("DESISTENTE", "Defaulted")]
        [InlineData("EXCLUIDO", "Defaulted")]
        [InlineData("desistente", "Defaulted")]
        [InlineData("excluido", "Defaulted")]
        [InlineData("paid_off", "Defaulted")] // Legacy
        public void MapStatus_DefaultedAliases_ShouldMapToDefaulted(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("InvalidStatus")]
        [InlineData("Unknown")]
        [InlineData("")]
        [InlineData(null)]
        public void MapStatus_InvalidInput_ShouldReturnNull(string? input)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("Active", true)]
        [InlineData("Late1", true)]
        [InlineData("Late2", true)]
        [InlineData("Late3", true)]
        [InlineData("Defaulted", true)]
        [InlineData("active", false)] // lowercase not valid
        [InlineData("Normal", false)] // alias not valid
        [InlineData("Invalid", false)]
        [InlineData(null, false)]
        public void IsValidStatus_ShouldValidateCorrectly(string? input, bool expected)
        {
            // Act
            var result = ContractStatusMapper.IsValidStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GetValidStatuses_ShouldReturnAllCanonicalStatuses()
        {
            // Act
            var statuses = ContractStatusMapper.GetValidStatuses();

            // Assert
            statuses.Should().HaveCount(5);
            statuses.Should().Contain("Active");
            statuses.Should().Contain("Late1");
            statuses.Should().Contain("Late2");
            statuses.Should().Contain("Late3");
            statuses.Should().Contain("Defaulted");
        }
    }
}
