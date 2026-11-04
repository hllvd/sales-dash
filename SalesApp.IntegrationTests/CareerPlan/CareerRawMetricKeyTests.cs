using FluentAssertions;
using SalesApp.CareerPlan;
using Xunit;

namespace SalesApp.IntegrationTests.CareerPlan
{
    public class CareerRawMetricKeyTests
    {
        [Theory]
        [InlineData(MetricKind.StrictRetention, 15, "strictRetention15m")]
        [InlineData(MetricKind.StrictRetention, 6, "strictRetention6m")]
        [InlineData(MetricKind.Retention, 12, "retention12m")]
        [InlineData(MetricKind.Production, 15, "production15m")]
        [InlineData(MetricKind.IsTeamOwner, null, "isTeamOwner")]
        [InlineData(MetricKind.ActiveTeamMembers, null, "activeTeamMembers")]
        [InlineData(MetricKind.DirectSubTeams, null, "directSubTeams")]
        [InlineData(MetricKind.StrictRetention, 9, null)]
        public void From_NoEligibility_MapsToConsolidatedCard(MetricKind metric, int? window, string? expected)
        {
            CareerRawMetricKey.From(metric, window, null).Should().Be(expected);
        }

        [Fact]
        public void From_ActiveProduction12mMinPaid2_MapsToActiveProductionCard()
        {
            CareerRawMetricKey.From(MetricKind.ActiveProduction, 12, new EligibilityFilter(MinInferredPaidMonths: 2))
                .Should().Be("activeProduction12mMinPaid2");
        }

        [Fact]
        public void From_ActiveProductionOtherWindow_ReturnsNull()
        {
            CareerRawMetricKey.From(MetricKind.ActiveProduction, 6, new EligibilityFilter(MinInferredPaidMonths: 2))
                .Should().BeNull();
        }

        [Fact]
        public void From_RetentionWithEligibilityFilter_ReturnsNull()
        {
            CareerRawMetricKey.From(MetricKind.StrictRetention, 15, new EligibilityFilter(MinAgeMonths: 2))
                .Should().BeNull();
        }
    }
}
