using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SalesApp.Controllers;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ScrapeCancellationTests
    {
        [Fact]
        public void ComputeFinalStatus_ReturnsCancelled_WhenNoActiveJobsAndAnyJobCancelled()
        {
            var jobs = new List<ScrapeLogEntry>
            {
                new ScrapeLogEntry { JobId = "j1", Status = "Succeeded" },
                new ScrapeLogEntry { JobId = "j2", Status = "Cancelled" }
            };

            ScrapeDynamoLogService.ComputeFinalStatus(jobs).Should().Be("Cancelled");
        }

        [Fact]
        public void ComputeFinalStatus_ReturnsRunning_WhenAnyJobStillRunning()
        {
            var jobs = new List<ScrapeLogEntry>
            {
                new ScrapeLogEntry { JobId = "j1", Status = "Cancelled" },
                new ScrapeLogEntry { JobId = "j2", Status = "Running" }
            };

            ScrapeDynamoLogService.ComputeFinalStatus(jobs).Should().Be("Running");
        }

        [Fact]
        public void MapToEntry_WhenCancelledBeyondTimeout_PreservesCancelledStatus()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "JobId", new AttributeValue { S = "job-cancel-old" } },
                { "Status", new AttributeValue { S = "Cancelled" } },
                { "CreatedAt", new AttributeValue { S = DateTime.UtcNow.AddHours(-5).ToString("o") } },
                { "CompletedAt", new AttributeValue { S = DateTime.UtcNow.AddHours(-4).ToString("o") } },
                { "ErrorMessage", new AttributeValue { S = "Cancelado manualmente" } }
            };

            var entry = ScrapeDynamoLogService.MapToEntry(item);
            entry.Status.Should().Be("Cancelled");
            entry.ErrorMessage.Should().Be("Cancelado manualmente");
        }

        [Fact]
        public void ScrapeController_CancelEndpoints_RequireSuperadminRole()
        {
            var cancelRunAttr = typeof(ScrapeController)
                .GetMethod(nameof(ScrapeController.CancelRun))!
                .GetCustomAttribute<AuthorizeAttribute>();
            var cancelJobAttr = typeof(ScrapeController)
                .GetMethod(nameof(ScrapeController.CancelJob))!
                .GetCustomAttribute<AuthorizeAttribute>();

            cancelRunAttr.Should().NotBeNull();
            cancelRunAttr!.Roles.Should().Be("superadmin");

            cancelJobAttr.Should().NotBeNull();
            cancelJobAttr!.Roles.Should().Be("superadmin");
        }

        [Fact]
        public async Task CancelRunAsync_SignalsActiveJobsAndSkipsCompletedJobs()
        {
            var logMock = new Mock<IScrapeDynamoLogService>();
            var importMock = new Mock<IScrapeImportService>(MockBehavior.Strict);
            var scraperClientMock = new Mock<PbiScraperClient>(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build());
            scraperClientMock.Setup(c => c.CancelJobAsync(It.IsAny<string>())).ReturnsAsync(true);

            logMock.Setup(l => l.GetRunDetailAsync("run-123", null)).ReturnsAsync(new ScrapeRunDetail
            {
                RunId = "run-123",
                Jobs = new List<ScrapeLogEntry>
                {
                    new ScrapeLogEntry { JobId = "job-active", RunId = "run-123", UserId = "u1", UserEmail = "sa@test.com", Store = "Consultor", Matricula = "6111", Status = "Running" },
                    new ScrapeLogEntry { JobId = "job-done", RunId = "run-123", UserId = "u1", UserEmail = "sa@test.com", Store = "Consultor", Matricula = "6111", Status = "Succeeded" }
                }
            });

            var orchestrator = CreateOrchestrator(logMock.Object, importMock.Object, scraperClientMock.Object);

            var count = await orchestrator.CancelRunAsync("run-123", "superadmin@salesapp.com");

            count.Should().Be(1);
            logMock.Verify(l => l.SignalCancelJobAsync("job-active", "run-123", "superadmin@salesapp.com"), Times.Once);
            logMock.Verify(l => l.WriteJobStatusAsync("job-active", "u1", "Cancelled", "Consultor", "6111", "run-123", "sa@test.com", It.IsAny<object>()), Times.Once);
            logMock.Verify(l => l.SignalCancelJobAsync("job-done", It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
            scraperClientMock.Verify(c => c.CancelJobAsync("job-active"), Times.Once);
        }

        [Fact]
        public async Task HandleCallbackAsync_WhenJobAlreadyCancelled_IgnoresLateSucceededCallbackAndSkipsImport()
        {
            var logMock = new Mock<IScrapeDynamoLogService>();
            var importMock = new Mock<IScrapeImportService>(MockBehavior.Strict);
            logMock.Setup(l => l.IsJobCancelledAsync("job-late")).ReturnsAsync(true);

            var orchestrator = CreateOrchestrator(logMock.Object, importMock.Object, null!);

            await orchestrator.HandleCallbackAsync(new ScrapeResult
            {
                JobId = "job-late",
                RunId = "run-late",
                UserId = Guid.NewGuid().ToString(),
                Status = "Succeeded",
                Matricula = "6111",
                RowCount = 500,
                FileRelativePath = "consultor_6111.csv"
            });

            logMock.Verify(l => l.WriteJobStatusAsync("job-late", It.IsAny<string>(), "Cancelled", It.IsAny<string>(), "6111", "run-late", null, It.IsAny<object>()), Times.Once);
            importMock.VerifyNoOtherCalls();
        }

        private static ScrapeOrchestrator CreateOrchestrator(
            IScrapeDynamoLogService logService,
            IScrapeImportService importService,
            PbiScraperClient scraperClient)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());
            var config = new ConfigurationBuilder().Build();

            return new ScrapeOrchestrator(
                dbContext,
                scraperClient,
                logService,
                importService,
                new EphemeralDataProtectionProvider(),
                config);
        }
    }
}
