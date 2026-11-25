using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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
            var unauthorizedAdminId = Guid.NewGuid();
            dbContext.Users.AddRange(
                new User { Id = superAdminId, InternalId = 1, Name = "SA", Email = "sa@salesapp.com", PasswordHash = "h", RoleId = 1 },
                new User { Id = otherUserId, InternalId = 2, Name = "Admin", Email = "admin@salesapp.com", PasswordHash = "h", RoleId = 2, CanAccessPowerBiScrape = true },
                new User { Id = unauthorizedAdminId, InternalId = 3, Name = "BlockedAdmin", Email = "blocked@salesapp.com", PasswordHash = "h", RoleId = 2, CanAccessPowerBiScrape = false }
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

            // 2. Authorized admin sees only their own remote config (Id = 2)
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

            // 3. Unauthorized admin (CanAccessPowerBiScrape = false) receives Forbid
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, unauthorizedAdminId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                        }, "TestAuth"))
                }
            };

            var blockedResult = await controller.GetMyConfigs();
            blockedResult.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task ScrapePermissions_AdminCanEditOwnButCannotCreateOrDelete_AndSuperAdminCanReassignOwnerAndLogsGoToOwner()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());

            var superAdminId = Guid.NewGuid();
            var adminOwnerId = Guid.NewGuid();
            dbContext.Users.AddRange(
                new User { Id = superAdminId, InternalId = 1, Name = "SuperAdmin", Email = "sa@salesapp.com", PasswordHash = "h", RoleId = 1 },
                new User { Id = adminOwnerId, InternalId = 2, Name = "AdminOwner", Email = "owner@salesapp.com", PasswordHash = "h", RoleId = 2, CanAccessPowerBiScrape = true }
            );
            await dbContext.SaveChangesAsync();

            var orchestratorMock = new Mock<IScrapeOrchestrator>();
            orchestratorMock
                .Setup(o => o.TriggerScrapeAsync(It.IsAny<int>(), true, It.IsAny<string?>(), "owner@salesapp.com", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync("job-owner-1");

            var logMock = new Mock<IScrapeDynamoLogService>();
            logMock.Setup(l => l.GetAllRunsAsync(It.IsAny<int>())).ReturnsAsync(new List<ScrapeRunSummary> { new ScrapeRunSummary { RunId = "r-all" } });
            logMock.Setup(l => l.GetRunsByUserAsync(adminOwnerId, It.IsAny<int>())).ReturnsAsync(new List<ScrapeRunSummary> { new ScrapeRunSummary { RunId = "r-owner" } });

            var scraperClientMock = new Mock<PbiScraperClient>(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build());
            scraperClientMock
                .Setup(c => c.TestAuthAsync("99887", "secret", null, true))
                .ReturnsAsync((true, true, "OK", new List<string>(), (string?)null, (string?)null));

            var controller = new ScrapeController(
                orchestratorMock.Object,
                logMock.Object,
                dbContext,
                Mock.Of<IScrapeImportService>(),
                scraperClientMock.Object,
                new EphemeralDataProtectionProvider(),
                new ConfigurationBuilder().Build(),
                Mock.Of<Microsoft.Extensions.Logging.ILogger<ScrapeController>>());

            // 1. Authorized Admin CANNOT create a new connector
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminOwnerId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                        }, "TestAuth"))
                }
            };

            var adminCreateRes = await controller.SaveConfig(new ScrapeConfigRequest
            {
                Matricula = "99887",
                PowerBiPassword = "secret",
                OutputMode = "sqs",
                TestOnSave = false
            });
            adminCreateRes.Should().BeOfType<ForbidResult>();

            // 2. SuperAdmin creates connector and assigns OwnerUserId to adminOwnerId
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

            var saCreateRes = await controller.SaveConfig(new ScrapeConfigRequest
            {
                Matricula = "99887",
                PowerBiPassword = "secret",
                OutputMode = "sqs",
                OwnerUserId = adminOwnerId,
                TestOnSave = false
            }) as OkObjectResult;
            saCreateRes.Should().NotBeNull();
            var createdDto = (ScrapeConfigDto)saCreateRes!.Value!;
            createdDto.OwnerUserId.Should().Be(adminOwnerId);
            createdDto.OwnerUserEmail.Should().Be("owner@salesapp.com");

            // 3. SuperAdmin triggers scrape on that connector -> log is attributed to owner@salesapp.com
            await controller.TriggerScrape(createdDto.Id);
            orchestratorMock.Verify(
                o => o.TriggerScrapeAsync(createdDto.Id, true, It.IsAny<string?>(), "owner@salesapp.com", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Once);

            // 4. SuperAdmin GetMyRuns returns all runs; Admin GetMyRuns returns only their own runs
            var saRuns = (await controller.GetMyRuns() as OkObjectResult)!.Value as List<ScrapeRunSummary>;
            saRuns.Should().ContainSingle(r => r.RunId == "r-all");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminOwnerId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                        }, "TestAuth"))
                }
            };

            var adminRuns = (await controller.GetMyRuns() as OkObjectResult)!.Value as List<ScrapeRunSummary>;
            adminRuns.Should().ContainSingle(r => r.RunId == "r-owner");

            // 5. Authorized Admin CAN edit their own connector, but CANNOT delete it
            var adminEditRes = await controller.SaveConfig(new ScrapeConfigRequest
            {
                Id = createdDto.Id,
                Matricula = "99887",
                OutputMode = "sqs",
                DefaultStartMonth = "2026-01",
                TestOnSave = false
            });
            adminEditRes.Should().BeOfType<OkObjectResult>();

            var adminDeleteRes = await controller.DeleteConfig(createdDto.Id);
            adminDeleteRes.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task GetAllRunsAsync_WhenGsi1FailsAndFirstScanPageIsEmpty_PaginatesScanToReturnAllRuns()
        {
            var dynamoMock = new Mock<IAmazonDynamoDB>();
            dynamoMock
                .Setup(d => d.QueryAsync(It.Is<QueryRequest>(r => r.IndexName == "GSI1"), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AmazonDynamoDBException("User is not authorized to perform: dynamodb:Query on resource: index/GSI1"));

            var firstPageKey = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = "ERROR#1" } },
                { "SK", new AttributeValue { S = "TS#2026-11-24" } }
            };

            var secondPageItems = new List<Dictionary<string, AttributeValue>>
            {
                new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "USER#11111111-1111-1111-1111-111111111111" } },
                    { "SK", new AttributeValue { S = "JOB#job-1" } },
                    { "JobId", new AttributeValue { S = "job-1" } },
                    { "RunId", new AttributeValue { S = "run-1" } },
                    { "UserId", new AttributeValue { S = "11111111-1111-1111-1111-111111111111" } },
                    { "UserEmail", new AttributeValue { S = "superadmin@salesapp.com" } },
                    { "Status", new AttributeValue { S = "Succeeded" } },
                    { "Matricula", new AttributeValue { S = "00001" } },
                    { "Store", new AttributeValue { S = "Loja A" } },
                    { "RowCount", new AttributeValue { N = "42" } },
                    { "CreatedAt", new AttributeValue { S = "2026-11-24T14:00:00Z" } }
                },
                new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "USER#22222222-2222-2222-2222-222222222222" } },
                    { "SK", new AttributeValue { S = "JOB#job-2" } },
                    { "JobId", new AttributeValue { S = "job-2" } },
                    { "RunId", new AttributeValue { S = "run-2" } },
                    { "UserId", new AttributeValue { S = "22222222-2222-2222-2222-222222222222" } },
                    { "UserEmail", new AttributeValue { S = "admin1@salesapp.com" } },
                    { "Status", new AttributeValue { S = "Succeeded" } },
                    { "Matricula", new AttributeValue { S = "00002" } },
                    { "Store", new AttributeValue { S = "Loja B" } },
                    { "RowCount", new AttributeValue { N = "15" } },
                    { "CreatedAt", new AttributeValue { S = "2026-11-24T14:05:00Z" } }
                }
            };

            dynamoMock
                .SetupSequence(d => d.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ScanResponse
                {
                    Items = new List<Dictionary<string, AttributeValue>>(),
                    LastEvaluatedKey = firstPageKey
                })
                .ReturnsAsync(new ScanResponse
                {
                    Items = secondPageItems,
                    LastEvaluatedKey = new Dictionary<string, AttributeValue>()
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AWS:DynamoDbTable"] = "pbi_scrape_logs" })
                .Build();
            var service = new ScrapeDynamoLogService(dynamoMock.Object, config, Mock.Of<ILogger<ScrapeDynamoLogService>>());

            var runs = await service.GetAllRunsAsync();

            runs.Should().HaveCount(2);
            runs.Select(r => r.UserEmail).Should().Contain(new[] { "superadmin@salesapp.com", "admin1@salesapp.com" });
            dynamoMock.Verify(d => d.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public void MapToEntry_ReadsS3BucketAndS3Key()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "JobId", new AttributeValue { S = "job-s3-1" } },
                { "RunId", new AttributeValue { S = "run-s3-1" } },
                { "Status", new AttributeValue { S = "Succeeded" } },
                { "s3Bucket", new AttributeValue { S = "hdev-sales-dash" } },
                { "s3Key", new AttributeValue { S = "scrape-results/file.csv" } },
                { "CreatedAt", new AttributeValue { S = "2026-11-24T16:00:00Z" } }
            };

            var entry = ScrapeDynamoLogService.MapToEntry(item);

            entry.S3Bucket.Should().Be("hdev-sales-dash");
            entry.S3Key.Should().Be("scrape-results/file.csv");
        }

        [Fact]
        public async Task DownloadRunFiles_AuthorizationAndFreshPresignedUrlAndMissingCases()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var dbContext = new AppDbContext(options, Mock.Of<IHttpContextAccessor>());

            var logMock = new Mock<IScrapeDynamoLogService>();
            var s3Mock = new Mock<Amazon.S3.IAmazonS3>();
            var httpClientFactory = Mock.Of<System.Net.Http.IHttpClientFactory>();
            var pbiClient = new PbiScraperClient(new System.Net.Http.HttpClient(), new ConfigurationBuilder().Build());

            var controller = new ScrapeController(
                Mock.Of<IScrapeOrchestrator>(),
                logMock.Object,
                dbContext,
                Mock.Of<IScrapeImportService>(),
                pbiClient,
                new EphemeralDataProtectionProvider(),
                new ConfigurationBuilder().Build(),
                Mock.Of<ILogger<ScrapeController>>(),
                s3Mock.Object);

            // 1. Admin (non-superadmin) -> Forbid
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                    }, "Test"))
                }
            };
            var adminResult = await controller.DownloadRunFiles("run-1");
            adminResult.Should().BeOfType<ForbidResult>();

            // 2. Superadmin -> generates fresh presigned URL each call
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "superadmin@salesapp.com"),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "superadmin")
                    }, "Test"))
                }
            };

            logMock.Setup(l => l.GetRunDetailAsync("run-1", null)).ReturnsAsync(new ScrapeRunDetail
            {
                RunId = "run-1",
                Jobs = new List<ScrapeLogEntry>
                {
                    new ScrapeLogEntry
                    {
                        JobId = "job-ok",
                        RunId = "run-1",
                        Status = "Succeeded",
                        Matricula = "0001",
                        S3Bucket = "hdev-sales-dash",
                        S3Key = "scrape-results/2026-11/out.csv",
                        CreatedAt = DateTime.UtcNow
                    },
                    new ScrapeLogEntry
                    {
                        JobId = "job-failed",
                        RunId = "run-1",
                        Status = "Failed",
                        S3Bucket = "hdev-sales-dash",
                        S3Key = "scrape-results/2026-11/fail.csv",
                        CreatedAt = DateTime.UtcNow
                    }
                }
            });

            s3Mock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<Amazon.S3.Model.GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.S3.Model.GetObjectMetadataResponse());

            int callCount = 0;
            s3Mock.Setup(s => s.GetPreSignedURL(It.IsAny<Amazon.S3.Model.GetPreSignedUrlRequest>()))
                .Returns(() => $"https://s3.amazonaws.com/hdev-sales-dash/out.csv?sig={++callCount}");

            var firstRes = await controller.DownloadRunFiles("run-1") as OkObjectResult;
            var secondRes = await controller.DownloadRunFiles("run-1") as OkObjectResult;

            firstRes.Should().NotBeNull();
            secondRes.Should().NotBeNull();
            var firstLinks = firstRes!.Value as List<ScrapeDownloadLinkDto>;
            var secondLinks = secondRes!.Value as List<ScrapeDownloadLinkDto>;
            firstLinks.Should().HaveCount(1);
            firstLinks![0].FileName.Should().Be("out.csv");
            firstLinks[0].Url.Should().Contain("sig=1");
            secondLinks![0].Url.Should().Contain("sig=2");

            // 3. Missing S3Key -> 404
            logMock.Setup(l => l.GetRunDetailAsync("run-no-key", null)).ReturnsAsync(new ScrapeRunDetail
            {
                RunId = "run-no-key",
                Jobs = new List<ScrapeLogEntry>
                {
                    new ScrapeLogEntry { JobId = "j1", Status = "Succeeded" }
                }
            });
            var noKeyRes = await controller.DownloadRunFiles("run-no-key");
            noKeyRes.Should().BeOfType<NotFoundObjectResult>();

            // 4. S3 returns NoSuchKey -> 404
            logMock.Setup(l => l.GetRunDetailAsync("run-missing-s3", null)).ReturnsAsync(new ScrapeRunDetail
            {
                RunId = "run-missing-s3",
                Jobs = new List<ScrapeLogEntry>
                {
                    new ScrapeLogEntry
                    {
                        JobId = "j2",
                        Status = "Succeeded",
                        S3Bucket = "hdev-sales-dash",
                        S3Key = "scrape-results/gone.csv"
                    }
                }
            });
            s3Mock.Setup(s => s.GetObjectMetadataAsync(
                    It.Is<Amazon.S3.Model.GetObjectMetadataRequest>(r => r.Key == "scrape-results/gone.csv"),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Amazon.S3.AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

            var missingS3Res = await controller.DownloadRunFiles("run-missing-s3");
            missingS3Res.Should().BeOfType<NotFoundObjectResult>();
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
