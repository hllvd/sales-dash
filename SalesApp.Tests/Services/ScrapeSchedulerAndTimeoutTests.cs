using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ScrapeSchedulerAndTimeoutTests
    {
        [Fact]
        public void ResolveScheduledScrapeDate_WhenDefaultStartMonthNullOrEmpty_ReturnsNull()
        {
            var now = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

            ScrapeSchedulerService.ResolveScheduledScrapeDate(null, "consultor", now).Should().BeNull();
            ScrapeSchedulerService.ResolveScheduledScrapeDate("", "consultor", now).Should().BeNull();
            ScrapeSchedulerService.ResolveScheduledScrapeDate("   ", "consultor", now).Should().BeNull();
        }

        [Fact]
        public void ResolveScheduledScrapeDate_WhenConsultorAndValidStartMonth_ReturnsJoinedMonths()
        {
            var now = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            var result = ScrapeSchedulerService.ResolveScheduledScrapeDate("2026-08", "consultor", now);

            result.Should().Be("2026-08,2026-09,2026-10");
        }

        [Fact]
        public void ResolveScheduledScrapeDate_WhenGeralAndValidStartMonth_ReturnsFirstMonth()
        {
            var now = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            var result = ScrapeSchedulerService.ResolveScheduledScrapeDate("2026-08", "geral", now);

            result.Should().Be("2026-08");
        }

        [Fact]
        public void ResolveScheduledScrapeDate_WhenStartMonthInFuture_ReturnsCurrentMonth()
        {
            var now = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            var result = ScrapeSchedulerService.ResolveScheduledScrapeDate("2026-12", "consultor", now);

            result.Should().Be("2026-10");
        }

        [Theory]
        [InlineData(null, 60)]
        [InlineData("", 60)]
        [InlineData("   ", 60)]
        [InlineData("2026-10", 8)] // 5 + (1 * 3)
        [InlineData("2026-08,2026-09,2026-10", 14)] // 5 + (3 * 3)
        [InlineData("2025-08,2025-09,2025-10,2025-11,2025-12,2026-01,2026-02,2026-03,2026-04,2026-05,2026-06,2026-07,2026-08,2026-09,2026-10", 50)] // 5 + (15 * 3)
        public void CalculateJobTimeoutMinutes_CalculatesExpectedTimeouts(string? scrapeDate, int expectedMinutes)
        {
            var minutes = ScrapeDynamoLogService.CalculateJobTimeoutMinutes(scrapeDate);
            minutes.Should().Be(expectedMinutes);
        }

        [Fact]
        public void MapToEntry_WhenPendingUnderTimeout_DoesNotMarkAsFailed()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "JobId", new AttributeValue { S = "job-1" } },
                { "Status", new AttributeValue { S = "Running" } },
                { "CreatedAt", new AttributeValue { S = DateTime.UtcNow.AddMinutes(-10).ToString("o") } },
                // 3 months -> timeout is 5 + (3 * 3) = 14 minutes. At 10 minutes, still running!
                { "ScrapeDate", new AttributeValue { S = "2026-08,2026-09,2026-10" } }
            };

            var parsed = ScrapeDynamoLogService.MapToEntry(item);
            parsed.Status.Should().Be("Running");
        }

        [Fact]
        public void MapToEntry_WhenPendingExceedsDynamicTimeout_MarksAsFailed()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "JobId", new AttributeValue { S = "job-2" } },
                { "Status", new AttributeValue { S = "Pending" } },
                { "CreatedAt", new AttributeValue { S = DateTime.UtcNow.AddMinutes(-16).ToString("o") } },
                // 3 months -> timeout is 14 minutes. At 16 minutes, it should timeout!
                { "ScrapeDate", new AttributeValue { S = "2026-08,2026-09,2026-10" } }
            };

            var parsed = ScrapeDynamoLogService.MapToEntry(item);
            parsed.Status.Should().Be("Failed");
            parsed.ErrorMessage.Should().Be("Tempo limite excedido (Timeout)");
        }

        [Fact]
        public void MapToEntry_WhenNoScrapeDate_Uses60MinuteTimeout()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "JobId", new AttributeValue { S = "job-3" } },
                { "Status", new AttributeValue { S = "Running" } },
                { "CreatedAt", new AttributeValue { S = DateTime.UtcNow.AddMinutes(-45).ToString("o") } }
                // No ScrapeDate -> timeout is 60 minutes. At 45 minutes, still running!
            };

            var parsed = ScrapeDynamoLogService.MapToEntry(item);
            parsed.Status.Should().Be("Running");
        }
    }
}
