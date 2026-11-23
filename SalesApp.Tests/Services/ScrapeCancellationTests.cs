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

        [Fact]
        public async Task TriggerScrape_WhenPreFlightAuthDetectsWrongPassword_ReturnsBadRequestAndLocksConfigWithoutEnqueueing()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                InternalId = 1,
                Name = "SuperAdmin",
                Email = "superadmin@salesapp.com",
                PasswordHash = "hash",
                RoleId = 1
            };
            dbContext.Users.Add(user);

            var config = new ScrapeConfig
            {
                Id = 77,
                UserInternalId = 1,
                Matricula = "13370",
                PowerBiPassword = "wrong-password-123",
                ScrapeType = "consultor",
                OutputMode = "sqs",
                CredentialStatus = null
            };
            dbContext.ScrapeConfigs.Add(config);
            await dbContext.SaveChangesAsync();

            var orchestratorMock = new Mock<IScrapeOrchestrator>(MockBehavior.Strict);
            var scraperClientMock = new Mock<PbiScraperClient>(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build());
            scraperClientMock
                .Setup(c => c.TestAuthAsync("13370", "wrong-password-123", null, true))
                .ReturnsAsync((false, false, "Usuário ou senha inválida no AVA PRO.", new List<string> { "[Falha de Autenticação] Usuário ou senha inválida no AVA PRO." }, (string?)null, (string?)null));

            var controller = new ScrapeController(
                orchestratorMock.Object,
                Mock.Of<IScrapeDynamoLogService>(),
                dbContext,
                Mock.Of<IScrapeImportService>(),
                scraperClientMock.Object,
                new EphemeralDataProtectionProvider(),
                new ConfigurationBuilder().Build(),
                Mock.Of<Microsoft.Extensions.Logging.ILogger<ScrapeController>>());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "superadmin@salesapp.com"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "superadmin")
                        }, "TestAuth"))
                }
            };

            var actionResult = await controller.TriggerScrape(77);

            actionResult.Should().BeOfType<BadRequestObjectResult>();
            var updatedConfig = await dbContext.ScrapeConfigs.FindAsync(77);
            updatedConfig!.CredentialStatus.Should().Be("wrong-password");
            orchestratorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SaveConfig_WithTestOnSave_DistinguishesWrongPasswordFromTimeout()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());
            var userId = Guid.NewGuid();
            dbContext.Users.Add(new User
            {
                Id = userId,
                InternalId = 1,
                Name = "SuperAdmin",
                Email = "superadmin@salesapp.com",
                PasswordHash = "hash",
                RoleId = 1
            });
            await dbContext.SaveChangesAsync();

            var scraperClientMock = new Mock<PbiScraperClient>(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build());
            scraperClientMock
                .Setup(c => c.TestAuthAsync("13370", "bad-pass", null, false))
                .ReturnsAsync((false, false, "Usuário ou senha inválida no AVA PRO.", new List<string> { "Step 1" }, (string?)null, (string?)null));
            scraperClientMock
                .Setup(c => c.TestAuthAsync("13370", "slow-pass", null, false))
                .ReturnsAsync((false, true, "Tempo limite excedido ao testar credenciais no AVA PRO (25s). Tente novamente.", new List<string> { "[Timeout]" }, (string?)null, (string?)null));

            var controller = new ScrapeController(
                Mock.Of<IScrapeOrchestrator>(),
                Mock.Of<IScrapeDynamoLogService>(),
                dbContext,
                Mock.Of<IScrapeImportService>(),
                scraperClientMock.Object,
                new EphemeralDataProtectionProvider(),
                new ConfigurationBuilder().Build(),
                Mock.Of<Microsoft.Extensions.Logging.ILogger<ScrapeController>>());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "superadmin")
                        }, "TestAuth"))
                }
            };

            var wrongPassRes = await controller.SaveConfig(new ScrapeConfigRequest
            {
                Matricula = "13370",
                PowerBiPassword = "bad-pass",
                ScrapeType = "consultor",
                TestOnSave = true
            }) as BadRequestObjectResult;

            wrongPassRes.Should().NotBeNull();
            var wrongPassJson = Newtonsoft.Json.JsonConvert.SerializeObject(wrongPassRes!.Value);
            wrongPassJson.Should().Contain("\"errorType\":\"wrong-password\"");
            wrongPassJson.Should().Contain("Senha recusada pelo AVA PRO");

            var timeoutRes = await controller.SaveConfig(new ScrapeConfigRequest
            {
                Matricula = "13370",
                PowerBiPassword = "slow-pass",
                ScrapeType = "consultor",
                TestOnSave = true
            }) as BadRequestObjectResult;

            timeoutRes.Should().NotBeNull();
            var timeoutJson = Newtonsoft.Json.JsonConvert.SerializeObject(timeoutRes!.Value);
            timeoutJson.Should().Contain("\"errorType\":\"timeout\"");
            timeoutJson.Should().Contain("Timeout ao validar senha no AVA PRO");
        }

        [Fact]
        public async Task GetMyConfigs_WhenSuperAdmin_ReturnsAllRemoteConfigsAndExcludesLocal()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());

            var superAdminId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            dbContext.Users.AddRange(
                new User { Id = superAdminId, InternalId = 1, Name = "SA", Email = "sa@salesapp.com", PasswordHash = "h", RoleId = 1 },
                new User { Id = otherUserId, InternalId = 2, Name = "Admin", Email = "admin@salesapp.com", PasswordHash = "h", RoleId = 2 }
            );
            dbContext.ScrapeConfigs.AddRange(
                new ScrapeConfig { Id = 1, UserInternalId = 1, Matricula = "11111", OutputMode = "sqs", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
                new ScrapeConfig { Id = 2, UserInternalId = 2, Matricula = "22222", OutputMode = "sqs", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new ScrapeConfig { Id = 3, UserInternalId = null, Matricula = "33333", OutputMode = "sqs", CreatedAt = DateTime.UtcNow },
                new ScrapeConfig { Id = 4, UserInternalId = 1, Matricula = "44444-LOCAL", OutputMode = "direct", CreatedAt = DateTime.UtcNow }
            );
            await dbContext.SaveChangesAsync();

            var controller = new ScrapeController(
                Mock.Of<IScrapeOrchestrator>(),
                Mock.Of<IScrapeDynamoLogService>(),
                dbContext,
                Mock.Of<IScrapeImportService>(),
                new Mock<PbiScraperClient>(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build()).Object,
                new EphemeralDataProtectionProvider(),
                new ConfigurationBuilder().Build(),
                Mock.Of<Microsoft.Extensions.Logging.ILogger<ScrapeController>>());

            // 1. Superadmin sees all 3 remote ("sqs") configs and excludes local ("direct") config
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, superAdminId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "superadmin")
                        }, "TestAuth"))
                }
            };

            var saResult = await controller.GetMyConfigs() as OkObjectResult;
            saResult.Should().NotBeNull();
            var saConfigs = System.Linq.Enumerable.ToList((IEnumerable<ScrapeConfigDto>)saResult!.Value!);
            saConfigs.Should().HaveCount(3);
            saConfigs.Should().OnlyContain(c => c.OutputMode == "sqs");

            // 2. Regular admin sees only their own remote config (Id = 2)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, otherUserId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                        }, "TestAuth"))
                }
            };

            var adminResult = await controller.GetMyConfigs() as OkObjectResult;
            adminResult.Should().NotBeNull();
            var adminConfigs = System.Linq.Enumerable.ToList((IEnumerable<ScrapeConfigDto>)adminResult!.Value!);
            adminConfigs.Should().HaveCount(1);
            adminConfigs[0].Matricula.Should().Be("22222");
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
