using FluentAssertions;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests
{
    public class ContractStatusMapperTests
    {
        [Theory]
        [InlineData("Active", "active")]
        [InlineData("Normal", "active")]
        [InlineData("NORMAL", "active")]
        [InlineData("active", "active")]
        public void MapStatus_ActiveAliases_ShouldMapToActive(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late1", "late1")]
        [InlineData("NCONT 1 AT", "late1")]
        [InlineData("ncont 1 at", "late1")]
        [InlineData("CONT NÃO ENTREGUE 1 ATR", "late1")]
        public void MapStatus_Late1Aliases_ShouldMapToLate1(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late2", "late2")]
        [InlineData("NCONT 2 AT", "late2")]
        [InlineData("ncont 2 at", "late2")]
        [InlineData("CONT NÃO ENTREGUE 2 ATR", "late2")]
        public void MapStatus_Late2Aliases_ShouldMapToLate2(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late3", "late3")]
        [InlineData("NCONT 3 AT", "late3")]
        [InlineData("SUJ. A CANCELAMENTO", "late3")]
        [InlineData("SUJ. A  CANCELAMENTO", "late3")] // Double space
        [InlineData("suj. a cancelamento", "late3")]
        [InlineData("delinquent", "late3")] // Legacy
        public void MapStatus_Late3Aliases_ShouldMapToLate3(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Defaulted", "defaulted")]
        [InlineData("DESISTENTE", "defaulted")]
        [InlineData("EXCLUIDO", "defaulted")]
        [InlineData("desistente", "defaulted")]
        [InlineData("excluido", "defaulted")]
        [InlineData("paid_off", "defaulted")] // Legacy
        public void MapStatus_DefaultedAliases_ShouldMapToDefaulted(string input, string expected)
        {
            // Act
            var result = ContractStatusMapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Transferred", "transferred")]
        [InlineData("TRANSFERIDO", "transferred")]
        [InlineData("transferred", "transferred")]
        public void MapStatus_TransferredAliases_ShouldMapToTransferred(string input, string expected)
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
        [InlineData("Transferred", true)]
        [InlineData("active", true)] // lowercase IS valid now
        [InlineData("transferred", true)]
        [InlineData("Normal", false)] // alias not valid (only canonical)
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
            statuses.Should().HaveCount(6);
            statuses.Should().Contain("active");
            statuses.Should().Contain("late1");
            statuses.Should().Contain("late2");
            statuses.Should().Contain("late3");
            statuses.Should().Contain("defaulted");
            statuses.Should().Contain("transferred");
        }
    }
}
