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
                    { "Late1", new List<string> { "NCONT 1 AT", "CONT NÃO ENTREGUE 1 ATR" } },
                    { "Late2", new List<string> { "NCONT 2 AT", "CONT NÃO ENTREGUE 2 ATR" } },
                    { "Late3", new List<string> { "NCONT 3 AT", "SUJ. A CANCELAMENTO", "SUJ. A  CANCELAMENTO", "delinquent" } },
                    { "Defaulted", new List<string> { "DESISTENTE", "EXCLUIDO", "excluido", "paid_off" } },
                    { "Transferred", new List<string> { "TRANSFERIDO", "transferred" } },
                    { "AwaitingPayment", new List<string> { "AwaitingPayment", "SUJ. CANC. 1ª PARC. SEM PGTO" } }
                }
            });

            _mapper = new ContractStatusMapper(options);
        }

        [Theory]
        [InlineData("Active", "Active")]
        [InlineData("Normal", "Active")]
        [InlineData("NORMAL", "Active")]
        [InlineData("active", "Active")]
        public void MapStatus_ActiveAliases_ShouldMapToActive(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late1", "Late1")]
        [InlineData("NCONT 1 AT", "Late1")]
        [InlineData("ncont 1 at", "Late1")]
        [InlineData("CONT NÃO ENTREGUE 1 ATR", "Late1")]
        public void MapStatus_Late1Aliases_ShouldMapToLate1(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Late2", "Late2")]
        [InlineData("NCONT 2 AT", "Late2")]
        [InlineData("ncont 2 at", "Late2")]
        [InlineData("CONT NÃO ENTREGUE 2 ATR", "Late2")]
        public void MapStatus_Late2Aliases_ShouldMapToLate2(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

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
            var result = _mapper.MapStatus(input);

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
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Transferred", "Transferred")]
        [InlineData("TRANSFERIDO", "Transferred")]
        [InlineData("transferred", "Transferred")]
        public void MapStatus_TransferredAliases_ShouldMapToTransferred(string input, string expected)
        {
            // Act
            var result = _mapper.MapStatus(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("AwaitingPayment", "AwaitingPayment")]
        [InlineData("SUJ. CANC. 1ª PARC. SEM PGTO", "AwaitingPayment")]
        [InlineData("suj. canc. 1ª parc. sem pgto", "AwaitingPayment")]
        public void MapStatus_AwaitingPaymentAliases_ShouldMapToAwaitingPayment(string input, string expected)
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
        [InlineData("Late2", true)]
        [InlineData("Late3", true)]
        [InlineData("Defaulted", true)]
        [InlineData("Transferred", true)]
        [InlineData("AwaitingPayment", true)]
        [InlineData("active", true)]       // lowercase IS valid now
        [InlineData("transferred", true)]
        [InlineData("Normal", true)]       // alias IS valid
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
            statuses.Should().HaveCount(7);
            statuses.Should().Contain("Active");
            statuses.Should().Contain("Late1");
            statuses.Should().Contain("Late2");
            statuses.Should().Contain("Late3");
            statuses.Should().Contain("Defaulted");
            statuses.Should().Contain("Transferred");
            statuses.Should().Contain("AwaitingPayment");
        }
    }
}
