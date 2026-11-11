using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SalesApp.Controllers;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ScrapeCallbackSecurityTests
    {
        [Theory]
        [InlineData(null, "secret123", false)]
        [InlineData("", "secret123", false)]
        [InlineData("   ", "secret123", false)]
        [InlineData("secret123", null, false)]
        [InlineData("secret123", "", false)]
        [InlineData("secret123", "wrong", false)]
        [InlineData("secret123", "secret123", true)]
        [InlineData("secret123", " secret123 ", true)]
        public void IsWorkerSecretValid_EnforcesFailClosedConstantTimeCheck(
            string? expected,
            string? provided,
            bool expectedResult)
        {
            var actual = ScrapeController.IsWorkerSecretValid(expected, provided);
            actual.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("consultor_6111.csv", true)]
        [InlineData("subdir/consultor_6111.csv", true)]
        [InlineData("../../../../app/keys", false)]
        [InlineData("../../../keys/key-0000.xml", false)]
        [InlineData("subdir/../../escape.csv", false)]
        [InlineData("/etc/passwd", false)]
        [InlineData("\\app\\keys\\key.xml", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void TryResolveSafeOutputPath_BlocksTraversalAndRootedPaths(
            string? relativePath,
            bool expectedValid)
        {
            var outputDir = "/tmp/scrape-outputs-test";

            var isValid = ScrapeOrchestrator.TryResolveSafeOutputPath(
                outputDir,
                relativePath,
                out var resolvedPath,
                out var error);

            isValid.Should().Be(expectedValid);
            if (expectedValid)
            {
                error.Should().BeNull();
                resolvedPath.Should().StartWith(Path.GetFullPath(outputDir));
            }
            else
            {
                error.Should().NotBeNullOrWhiteSpace();
                resolvedPath.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task HandleCallback_WhenWorkerSecretMissingOrUnconfigured_ReturnsUnauthorizedWithoutCallingOrchestrator()
        {
            var orchestratorMock = new Mock<IScrapeOrchestrator>(MockBehavior.Strict);
            var controller = CreateController(orchestratorMock.Object, configuredSecret: null, providedSecret: null);

            var response = await controller.HandleCallback(new ScrapeResult
            {
                JobId = "probe-1",
                Status = "Succeeded",
                Matricula = "6111"
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
            orchestratorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleCallback_WhenWorkerSecretInvalid_ReturnsUnauthorized()
        {
            var orchestratorMock = new Mock<IScrapeOrchestrator>(MockBehavior.Strict);
            var controller = CreateController(orchestratorMock.Object, configuredSecret: "super-secret-value", providedSecret: "wrong");

            var response = await controller.HandleCallback(new ScrapeResult
            {
                JobId = "probe-2",
                Status = "Succeeded",
                Matricula = "6111"
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
            orchestratorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleCallback_WhenWorkerSecretMatches_CallsOrchestratorAndReturnsOk()
        {
            var orchestratorMock = new Mock<IScrapeOrchestrator>(MockBehavior.Strict);
            orchestratorMock
                .Setup(o => o.HandleCallbackAsync(It.Is<ScrapeResult>(r => r.JobId == "job-valid")))
                .Returns(Task.CompletedTask);

            var controller = CreateController(orchestratorMock.Object, configuredSecret: "super-secret-value", providedSecret: "super-secret-value");

            var response = await controller.HandleCallback(new ScrapeResult
            {
                JobId = "job-valid",
                Status = "Succeeded",
                Matricula = "6111"
            });

            response.Should().BeOfType<OkResult>();
            orchestratorMock.Verify(o => o.HandleCallbackAsync(It.IsAny<ScrapeResult>()), Times.Once);
        }

        [Fact]
        public async Task ImportFromS3_WhenWorkerSecretUnconfigured_ReturnsUnauthorized()
        {
            var orchestratorMock = new Mock<IScrapeOrchestrator>(MockBehavior.Strict);
            var controller = CreateController(orchestratorMock.Object, configuredSecret: "", providedSecret: "anything");

            var response = await controller.ImportFromS3(new ImportFromS3Request
            {
                S3Bucket = "bucket",
                S3Key = "key.csv"
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
        }

        private static ScrapeController CreateController(
            IScrapeOrchestrator orchestrator,
            string? configuredSecret,
            string? providedSecret)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());

            var configValues = new Dictionary<string, string?>();
            if (configuredSecret != null)
            {
                configValues["PbiScraper:WorkerSecret"] = configuredSecret;
            }
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var controller = new ScrapeController(
                orchestrator,
                Mock.Of<IScrapeDynamoLogService>(),
                dbContext,
                Mock.Of<IScrapeImportService>(),
                null!,
                new EphemeralDataProtectionProvider(),
                configuration,
                Mock.Of<ILogger<ScrapeController>>());

            var httpContext = new DefaultHttpContext();
            if (providedSecret != null)
            {
                httpContext.Request.Headers["X-Worker-Secret"] = providedSecret;
            }
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }
    }
}
