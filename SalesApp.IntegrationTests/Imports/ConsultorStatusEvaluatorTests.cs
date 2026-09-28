using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.Models;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    public class ConsultorStatusEvaluatorTests
    {
        private static readonly DateTime ReferenceDate = new DateTime(2026, 9, 28);

        [Theory]
        [InlineData("2 Rel Carteira.Definição.Situação", true)]
        [InlineData("Definição.Situação", true)]
        [InlineData("Sum(2 Rel Carteira.Quant.Parcelas em Atraso)", true)]
        [InlineData("Quant.Parcelas em Atraso", true)]
        [InlineData("Sum(2 Rel Carteira.Quant.Parcelas Pagas)", true)]
        [InlineData("Quant.Parcelas Pagas", true)]
        [InlineData("Situação Cobrança", false)]
        [InlineData("status_cota", false)]
        [InlineData("random_column", false)]
        public void IsConsultorRow_ShouldDetectConsultorColumnsCorrectly(string columnName, bool expected)
        {
            var row = new Dictionary<string, string> { { columnName, "val" } };
            ConsultorStatusEvaluator.IsConsultorRow(row).Should().Be(expected);
        }

        [Fact]
        public void IsConsultorRow_NullOrEmpty_ShouldReturnFalse()
        {
            ConsultorStatusEvaluator.IsConsultorRow(null).Should().BeFalse();
            ConsultorStatusEvaluator.IsConsultorRow(new Dictionary<string, string>()).Should().BeFalse();
        }

        [Theory]
        [InlineData("Excluido", 0, "Desistente")]
        [InlineData("EXCLUIDO", 0, "Desistente")]
        [InlineData("Excluído", 0, "Desistente")]
        [InlineData("Excluido", 1, "Defaulted")]
        [InlineData("Excluido", 5, "Defaulted")]
        [InlineData("Excluido", null, "Defaulted")]
        public void DeriveStatus_ExcluidoStatus_ShouldDifferentiateDesistenteAndDefaulted(
            string rawStatus, int? paidInstallments, string expectedStatus)
        {
            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: rawStatus,
                overdueInstallments: null,
                paidInstallments: paidInstallments,
                hasPayment: false,
                saleStartDate: null,
                referenceDate: ReferenceDate);

            result.Should().Be(expectedStatus);
        }

        [Theory]
        [InlineData(10, "AwaitingPayment")] // Purchased 10 days ago (<= 30 days) -> AwaitingPayment
        [InlineData(30, "AwaitingPayment")] // Purchased 30 days ago (<= 30 days) -> AwaitingPayment
        public void DeriveStatus_NormalWithoutPayment_RecentPurchase_ShouldBeAwaitingPayment(
            int daysAgo, string expectedStatus)
        {
            var saleDate = ReferenceDate.AddDays(-daysAgo);

            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: "Normal",
                overdueInstallments: 2, // Even if overdue is set, if <= 30 days, waiting payment
                paidInstallments: 0,
                hasPayment: false,
                saleStartDate: saleDate,
                referenceDate: ReferenceDate);

            result.Should().Be(expectedStatus);
        }

        [Theory]
        [InlineData(1, "Late1")]
        [InlineData(2, "Late2")]
        [InlineData(3, "Late3")]
        [InlineData(4, "Late3")]
        [InlineData(10, "Late3")]
        public void DeriveStatus_NormalWithoutPayment_OlderPurchase_ShouldDeriveLateStatuses(
            int overdueInstallments, string expectedStatus)
        {
            var saleDate = ReferenceDate.AddDays(-60); // 60 days ago (> 30 days)

            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: "Normal",
                overdueInstallments: overdueInstallments,
                paidInstallments: 0,
                hasPayment: false,
                saleStartDate: saleDate,
                referenceDate: ReferenceDate);

            result.Should().Be(expectedStatus);
        }

        [Fact]
        public void DeriveStatus_NormalWithoutPayment_OlderPurchase_NoOverdue_ShouldBeAwaitingPayment()
        {
            var saleDate = ReferenceDate.AddDays(-60);

            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: "Normal",
                overdueInstallments: 0,
                paidInstallments: 0,
                hasPayment: false,
                saleStartDate: saleDate,
                referenceDate: ReferenceDate);

            result.Should().Be("AwaitingPayment");
        }

        [Theory]
        [InlineData(0, "Active")]
        [InlineData(null, "Active")]
        [InlineData(1, "Late1")]
        [InlineData(2, "Late2")]
        [InlineData(3, "Late3")]
        [InlineData(5, "Late3")]
        public void DeriveStatus_NormalWithPayment_ShouldDeriveActiveOrLateStatuses(
            int? overdueInstallments, string expectedStatus)
        {
            var saleDate = ReferenceDate.AddDays(-120);

            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: "Normal",
                overdueInstallments: overdueInstallments,
                paidInstallments: 3,
                hasPayment: true,
                saleStartDate: saleDate,
                referenceDate: ReferenceDate);

            result.Should().Be(expectedStatus);
        }

        [Fact]
        public void DeriveStatus_OtherStatus_ShouldFallbackToDefaultMapper()
        {
            var result = ConsultorStatusEvaluator.DeriveStatus(
                rawStatus: "QUITADO",
                overdueInstallments: 0,
                paidInstallments: 50,
                hasPayment: true,
                saleStartDate: ReferenceDate.AddDays(-300),
                referenceDate: ReferenceDate,
                defaultStatusMapper: s => "CustomMapped");

            result.Should().Be("CustomMapped");
        }
    }
}
