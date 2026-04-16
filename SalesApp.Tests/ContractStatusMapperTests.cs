using FluentAssertions;
using Microsoft.Extensions.Options;
using SalesApp.Models.Configuration;
using SalesApp.Services;
using System.Collections.Generic;
using Xunit;

namespace SalesApp.Tests
{
    public class ContractStatusMapperTests
    {
        private readonly IContractStatusMapper _mapper;

        public ContractStatusMapperTests()
        {
            // Setup mock options for testing
            var options = Options.Create(new ContractStatusOptions
            {
                Mappings = new Dictionary<string, List<string>>
                {
                    { "Active", new List<string> { "Normal", "Ativa", "Ativo" } },
                    { "Late1", new List<string> { "NCONT 1 AT", "SUJ. CANC. 1ª PARC. SEM PGTO" } },
                    { "Late2", new List<string> { "NCONT 2 AT" } },
                    { "Late3", new List<string> { "NCONT 3 AT", "delinquent" } },
                    { "Defaulted", new List<string> { "DESISTENTE", "paid_off" } },
                    { "Transferred", new List<string> { "TRANSFERIDO" } }
                }
            });

            _mapper = new ContractStatusMapper(options);
        }

        [Theory]
        [InlineData("Active", "active")]
        [InlineData("Normal", "active")]
        [InlineData("NORMAL", "active")]
        [InlineData("active", "active")]
        public void MapStatus_ActiveAliases_ShouldMapToActive(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late1", "late1")]
        [InlineData("NCONT 1 AT", "late1")]
        [InlineData("SUJ. CANC. 1ª PARC. SEM PGTO", "late1")]
        public void MapStatus_Late1Aliases_ShouldMapToLate1(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late2", "late2")]
        [InlineData("NCONT 2 AT", "late2")]
        public void MapStatus_Late2Aliases_ShouldMapToLate2(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late3", "late3")]
        [InlineData("NCONT 3 AT", "late3")]
        [InlineData("delinquent", "late3")]
        public void MapStatus_Late3Aliases_ShouldMapToLate3(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Defaulted", "defaulted")]
        [InlineData("DESISTENTE", "defaulted")]
        [InlineData("paid_off", "defaulted")]
        public void MapStatus_DefaultedAliases_ShouldMapToDefaulted(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

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
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("Active", true)]
        [InlineData("Late1", true)]
        [InlineData("Normal", true)] // alias IS valid
        [InlineData("Invalid", false)]
        [InlineData(null, false)]
        public void IsValidStatus_ShouldValidateCorrectly(string? input, bool expected)
        {
            // Act
            var result = _mapper.IsValidStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GetValidStatuses_ShouldReturnAllCanonicalStatuses()
        {
            // Act
            var statuses = _mapper.GetValidStatuses();

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
