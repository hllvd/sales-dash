using System;
using FluentAssertions;
using SalesApp.ReportFilters.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    /// <summary>
    /// Unit tests for TeamMembershipResolver.IsMembershipActiveForSale.
    /// Pure function tests — no database, no mocks, zero side effects.
    /// </summary>
    public class TeamMembershipResolverTests
    {
        private static readonly DateTime ReportStart = new DateTime(2026, 1, 1);
        private static readonly DateTime ReportEnd = new DateTime(2026, 1, 31);

        [Fact]
        public void IsMembershipActiveForSale_WhenSaleIsWithinReportAndMembershipIsOngoing_ShouldReturnTrue()
        {
            // Arrange
            var membershipStart = new DateTime(2025, 12, 1);
            DateTime? membershipEnd = null;
            var saleStart = new DateTime(2026, 1, 15);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsMembershipActiveForSale_WhenSaleIsWithinReportAndMembershipIsClosedButActive_ShouldReturnTrue()
        {
            // Arrange
            var membershipStart = new DateTime(2025, 12, 1);
            DateTime? membershipEnd = new DateTime(2026, 1, 20);
            var saleStart = new DateTime(2026, 1, 15);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsMembershipActiveForSale_WhenSaleIsBeforeReportStart_ShouldReturnFalse()
        {
            // Arrange
            var membershipStart = new DateTime(2025, 12, 1);
            DateTime? membershipEnd = null;
            var saleStart = new DateTime(2025, 12, 25);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMembershipActiveForSale_WhenSaleIsAfterReportEnd_ShouldReturnFalse()
        {
            // Arrange
            var membershipStart = new DateTime(2025, 12, 1);
            DateTime? membershipEnd = null;
            var saleStart = new DateTime(2026, 2, 5);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMembershipActiveForSale_WhenMembershipStartsAfterSale_ShouldReturnFalse()
        {
            // Arrange
            var membershipStart = new DateTime(2026, 1, 20);
            DateTime? membershipEnd = null;
            var saleStart = new DateTime(2026, 1, 15);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMembershipActiveForSale_WhenMembershipEndedBeforeSale_ShouldReturnFalse()
        {
            // Arrange
            var membershipStart = new DateTime(2025, 12, 1);
            DateTime? membershipEnd = new DateTime(2026, 1, 10);
            var saleStart = new DateTime(2026, 1, 15);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                membershipStart,
                membershipEnd,
                saleStart,
                ReportStart,
                ReportEnd);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        // membershipStart, membershipEnd, saleStart, reportStart, reportEnd, expected
        [InlineData("2026-01-01", null,         "2026-01-01", "2026-01-01", null,         true)]  // Boundary: sale exactly on start
        [InlineData("2026-01-01", "2026-01-01", "2026-01-01", "2026-01-01", null,         false)] // Boundary: membership ends exactly on sale (exclusive end date)
        [InlineData("2026-01-02", null,         "2026-01-01", "2026-01-01", null,         false)] // Sale before membership starts
        [InlineData("2025-01-01", "2025-12-31", "2026-01-01", "2026-01-01", null,         false)] // Membership ended before report range
        public void IsMembershipActiveForSale_Theory(
            string memberStartStr,
            string? memberEndStr,
            string saleStartStr,
            string reportStartStr,
            string? reportEndStr,
            bool expected)
        {
            // Arrange
            var memberStart = DateTime.Parse(memberStartStr);
            DateTime? memberEnd = string.IsNullOrEmpty(memberEndStr) ? null : DateTime.Parse(memberEndStr);
            var saleStart = DateTime.Parse(saleStartStr);
            var reportStart = DateTime.Parse(reportStartStr);
            DateTime? reportEnd = string.IsNullOrEmpty(reportEndStr) ? null : DateTime.Parse(reportEndStr);

            // Act
            var result = TeamMembershipResolver.IsMembershipActiveForSale(
                memberStart,
                memberEnd,
                saleStart,
                reportStart,
                reportEnd);

            // Assert
            result.Should().Be(expected);
        }
    }
}
