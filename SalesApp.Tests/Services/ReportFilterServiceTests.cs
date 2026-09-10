using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.ReportFilters.DTOs;
using SalesApp.ReportFilters.Models;
using SalesApp.ReportFilters.Repositories;
using SalesApp.ReportFilters.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ReportFilterServiceTests
    {
        private readonly Mock<IReportFilterRepository> _repositoryMock;
        private readonly Mock<IContractRepository> _contractRepositoryMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<ITeamRepository> _teamRepositoryMock;
        private readonly Mock<IClassificationLevelRepository> _classificationLevelRepositoryMock;
        private readonly Mock<IUserClassificationRepository> _userClassificationRepositoryMock;
        private readonly ReportFilterService _service;

        public ReportFilterServiceTests()
        {
            _repositoryMock = new Mock<IReportFilterRepository>();
            _contractRepositoryMock = new Mock<IContractRepository>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _teamRepositoryMock = new Mock<ITeamRepository>();
            _classificationLevelRepositoryMock = new Mock<IClassificationLevelRepository>();
            _userClassificationRepositoryMock = new Mock<IUserClassificationRepository>();

            _service = new ReportFilterService(
                _repositoryMock.Object,
                _contractRepositoryMock.Object,
                _userRepositoryMock.Object,
                _teamRepositoryMock.Object,
                _classificationLevelRepositoryMock.Object,
                _userClassificationRepositoryMock.Object
            );
        }

        [Fact]
        public async Task CreateAsync_ShouldMapAndSaveExportedFields()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var request = new CreateReportFilterRequest
            {
                Name = "Report with Exported Fields",
                Scope = "shared",
                FilterConfig = new FilterConfigRequest
                {
                    Teams = new List<int> { 10 }
                },
                OutputColumns = new List<OutputColumnRequest>
                {
                    new OutputColumnRequest { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 }
                },
                ExportedFields = new List<ExportedFieldRequest>
                {
                    new ExportedFieldRequest { FieldType = "teams", Label = "Selecione a Equipe" },
                    new ExportedFieldRequest { FieldType = "emails", Label = "Selecione o Vendedor" }
                }
            };

            ReportFilter? savedFilter = null;
            _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<ReportFilter>()))
                .Callback<ReportFilter>(f => savedFilter = f)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CreateAsync(callerId, request);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.ExportedFields.Should().HaveCount(2);
            result.Data.ExportedFields[0].FieldType.Should().Be("teams");
            result.Data.ExportedFields[0].Label.Should().Be("Selecione a Equipe");
            result.Data.ExportedFields[1].FieldType.Should().Be("emails");
            result.Data.ExportedFields[1].Label.Should().Be("Selecione o Vendedor");

            savedFilter.Should().NotBeNull();
            savedFilter!.ExportedFields.Should().HaveCount(2);
        }

        [Fact]
        public async Task ExecuteAsync_WithOverrideTeamIdsAndEmails_ShouldPassOverridesToContractQuery()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260803120000000-abcdef123456";

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "Base Report",
                Scope = "private",
                FilterConfig = new FilterConfig
                {
                    Teams = new List<int> { 1, 2 },
                    Emails = new List<string> { "base@test.com" }
                },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 }
                },
                ExportedFields = new List<ExportedField>
                {
                    new ExportedField { FieldType = "teams", Label = "Equipe" }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()))
                .ReturnsAsync(new List<Contract>());

            var overrideTeams = new List<int> { 99 };
            var overrideEmails = new List<string> { "override@test.com" };

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25, overrideTeams, overrideEmails);

            // Assert
            result.Success.Should().BeTrue();
            
            // Verify contract repo received overridden email
            _contractRepositoryMock.Verify(c => c.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<List<string>?>(),
                "override@test.com",
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()), Times.Once);
        }

        [Fact]
        public void GetAvailableColumns_ShouldIncludeUserActiveInUsersContractAndUsersMatricula()
        {
            // Act
            var available = _service.GetAvailableColumns();

            // Assert
            var usersContract = available.Sources.Find(s => s.Source == "Users_Contract");
            usersContract.Should().NotBeNull();
            usersContract!.Fields.Should().Contain("userActive");

            var usersMatricula = available.Sources.Find(s => s.Source == "Users_Matricula");
            usersMatricula.Should().NotBeNull();
            usersMatricula!.Fields.Should().Contain("userActive");
        }

        [Fact]
        public void ResolveUserActive_WhenUserIsNull_ShouldReturnDash()
        {
            // Act
            var result = ReportFilterService.ResolveUserActive(null);

            // Assert
            result.Should().Be("—");
        }

        [Fact]
        public void ResolveUserActive_WhenUserIsInactiveInDatabase_ShouldReturnNao()
        {
            // Arrange
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            var user = new User
            {
                IsActive = false,
                CreatedAt = now.AddDays(-20),
                LastAccessedAt = now.AddDays(-5)
            };

            // Act
            var result = ReportFilterService.ResolveUserActive(user, now);

            // Assert
            result.Should().Be("Não");
        }

        [Fact]
        public void ResolveUserActive_WhenUserCreatedAtIsLessThan15DaysAgo_ShouldReturnNao()
        {
            // Arrange
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            var user = new User
            {
                IsActive = true,
                CreatedAt = now.AddDays(-10), // only 10 days old
                LastAccessedAt = now.AddDays(-2)
            };

            // Act
            var result = ReportFilterService.ResolveUserActive(user, now);

            // Assert
            result.Should().Be("Não");
        }

        [Fact]
        public void ResolveUserActive_WhenUserNeverAccessed_ShouldReturnNao()
        {
            // Arrange
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            var user = new User
            {
                IsActive = true,
                CreatedAt = now.AddDays(-20),
                LastAccessedAt = null
            };

            // Act
            var result = ReportFilterService.ResolveUserActive(user, now);

            // Assert
            result.Should().Be("Não");
        }

        [Fact]
        public void ResolveUserActive_WhenLastAccessedIsMoreThan30DaysAgo_ShouldReturnNao()
        {
            // Arrange
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            var user = new User
            {
                IsActive = true,
                CreatedAt = now.AddDays(-60),
                LastAccessedAt = now.AddDays(-31)
            };

            // Act
            var result = ReportFilterService.ResolveUserActive(user, now);

            // Assert
            result.Should().Be("Não");
        }

        [Fact]
        public void ResolveUserActive_WhenAllCriteriaAreMet_ShouldReturnSim()
        {
            // Arrange
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            var user = new User
            {
                IsActive = true,
                CreatedAt = now.AddDays(-20),
                LastAccessedAt = now.AddDays(-5)
            };

            // Act
            var result = ReportFilterService.ResolveUserActive(user, now);

            // Assert
            result.Should().Be("Sim");
        }

        [Fact]
        public async Task ExecuteAsync_WithUserActiveColumn_ShouldProjectSimAndNaoCorrectly()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260825120000000-useractive01";
            var now = DateTime.UtcNow;

            var activeUser = new User
            {
                Name = "Active User",
                Email = "active@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-25),
                LastAccessedAt = now.AddDays(-2)
            };

            var inactiveUser = new User
            {
                Name = "Inactive User",
                Email = "inactive@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-5), // < 15 days
                LastAccessedAt = now.AddDays(-1)
            };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "User Active Test Report",
                Scope = "private",
                FilterConfig = new FilterConfig(),
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 },
                    new OutputColumn { Source = "Users_Contract", Field = "userActive", Label = "Usuário Ativo", Order = 2 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                new Contract { ContractNumber = "CTR-001", User = activeUser, TotalAmount = 1000m },
                new Contract { ContractNumber = "CTR-002", User = inactiveUser, TotalAmount = 2000m },
                new Contract { ContractNumber = "CTR-003", User = null, TotalAmount = 3000m }
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Rows.Should().HaveCount(3);

            result.Data.Rows[0]["Contrato"].Should().Be("CTR-001");
            result.Data.Rows[0]["Usuário Ativo"].Should().Be("Sim");

            result.Data.Rows[1]["Contrato"].Should().Be("CTR-002");
            result.Data.Rows[1]["Usuário Ativo"].Should().Be("Não");

            result.Data.Rows[2]["Contrato"].Should().Be("CTR-003");
            result.Data.Rows[2]["Usuário Ativo"].Should().Be("—");
        }

        [Fact]
        public async Task ExecuteAsync_WithCountActiveUsers_ShouldCalculateActiveAndInactiveUsersCorrectly()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260825120000000-countactive01";
            var now = DateTime.UtcNow;

            var activeUser1 = new User
            {
                Id = Guid.NewGuid(),
                Name = "Active User 1",
                Email = "active1@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-25),
                LastAccessedAt = now.AddDays(-2)
            };

            var activeUser2 = new User
            {
                Id = Guid.NewGuid(),
                Name = "Active User 2",
                Email = "active2@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-20),
                LastAccessedAt = now.AddDays(-3)
            };

            var inactiveUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Inactive User",
                Email = "inactive@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-5), // < 15 days
                LastAccessedAt = now.AddDays(-1)
            };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "Count Active Users Report",
                Scope = "private",
                CountActiveUsers = true,
                SumTotal = true,
                FilterConfig = new FilterConfig(),
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                new Contract { ContractNumber = "CTR-001", User = activeUser1, TotalAmount = 1000m },
                new Contract { ContractNumber = "CTR-002", User = activeUser1, TotalAmount = 1500m }, // Same user
                new Contract { ContractNumber = "CTR-003", User = activeUser2, TotalAmount = 2000m },
                new Contract { ContractNumber = "CTR-004", User = inactiveUser, TotalAmount = 2500m },
                new Contract { ContractNumber = "CTR-005", User = null, TotalAmount = 3000m } // Null user
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.ActiveUsersCount.Should().Be(2); // activeUser1, activeUser2
            result.Data.InactiveUsersCount.Should().Be(1); // inactiveUser
            result.Data.TotalSum.Should().Be(10000m);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCountActiveUsersIsDisabled_ShouldReturnNullUserCounts()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260825120000000-countactivedisabled";
            var now = DateTime.UtcNow;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Active User",
                Email = "active@test.com",
                IsActive = true,
                CreatedAt = now.AddDays(-25),
                LastAccessedAt = now.AddDays(-2)
            };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "Disabled Count Active Users Report",
                Scope = "private",
                CountActiveUsers = false,
                FilterConfig = new FilterConfig(),
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                new Contract { ContractNumber = "CTR-001", User = user, TotalAmount = 1000m }
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.ActiveUsersCount.Should().BeNull();
            result.Data.InactiveUsersCount.Should().BeNull();
        }
        [Fact]
        public async Task ExecuteAsync_WithAwaitingPaymentTrue_ShouldReturnOnlyActiveContractsWithNoPayment()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260907120000000-awaitpay001";

            var activeStatus = new ContractStatusEntity { Name = "Active" };
            var defaultedStatus = new ContractStatusEntity { Name = "Defaulted" };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "Awaiting Payment Test",
                Scope = "private",
                FilterConfig = new FilterConfig { AwaitingPayment = true },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                // Should be included: Active + HasPayment == false
                new Contract { ContractNumber = "CTR-001", ContractStatus = activeStatus, HasPayment = false, TotalAmount = 1000m },
                // Should be excluded: Active + HasPayment == true
                new Contract { ContractNumber = "CTR-002", ContractStatus = activeStatus, HasPayment = true, TotalAmount = 2000m },
                // Should be excluded: Defaulted + HasPayment == false
                new Contract { ContractNumber = "CTR-003", ContractStatus = defaultedStatus, HasPayment = false, TotalAmount = 500m },
                // Should be excluded: Active + HasPayment == null
                new Contract { ContractNumber = "CTR-004", ContractStatus = activeStatus, HasPayment = null, TotalAmount = 300m },
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Rows.Should().HaveCount(1);
            result.Data.Rows[0]["Contrato"].Should().Be("CTR-001");
        }

        [Fact]
        public async Task ExecuteAsync_WithAwaitingPaymentFalse_ShouldExcludeActiveContractsWithNoPayment()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260907120000000-awaitpay002";

            var activeStatus = new ContractStatusEntity { Name = "Active" };
            var defaultedStatus = new ContractStatusEntity { Name = "Defaulted" };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "Not Awaiting Payment Test",
                Scope = "private",
                FilterConfig = new FilterConfig { AwaitingPayment = false },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                // Should be excluded: Active + HasPayment == false (this IS awaiting payment)
                new Contract { ContractNumber = "CTR-001", ContractStatus = activeStatus, HasPayment = false, TotalAmount = 1000m },
                // Should be included: Active + HasPayment == true
                new Contract { ContractNumber = "CTR-002", ContractStatus = activeStatus, HasPayment = true, TotalAmount = 2000m },
                // Should be included: Defaulted + HasPayment == false
                new Contract { ContractNumber = "CTR-003", ContractStatus = defaultedStatus, HasPayment = false, TotalAmount = 500m },
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Rows.Should().HaveCount(2);
            result.Data.Rows.Select(r => r["Contrato"]).Should().NotContain("CTR-001");
        }

        [Fact]
        public async Task ExecuteAsync_WithAwaitingPaymentNull_ShouldReturnAllContracts()
        {
            // Arrange: no AwaitingPayment filter → all contracts pass through
            var callerId = Guid.NewGuid().ToString();
            var filterId = "20260907120000000-awaitpay003";

            var activeStatus = new ContractStatusEntity { Name = "Active" };

            var existingReport = new ReportFilter
            {
                UserId = callerId,
                FilterId = filterId,
                Name = "No Awaiting Payment Filter",
                Scope = "private",
                FilterConfig = new FilterConfig { AwaitingPayment = null },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contrato", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(existingReport);

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team>());

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var contracts = new List<Contract>
            {
                new Contract { ContractNumber = "CTR-001", ContractStatus = activeStatus, HasPayment = false, TotalAmount = 1000m },
                new Contract { ContractNumber = "CTR-002", ContractStatus = activeStatus, HasPayment = true,  TotalAmount = 2000m },
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()))
                .ReturnsAsync(contracts);

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Rows.Should().HaveCount(2, "no filter applied when AwaitingPayment is null");
        }

        [Fact]
        public async Task ExecuteAsync_WithCurrentTeamFilter_ShouldPassUserInternalIdsToQuery()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "optim-filter-1";

            var report = new ReportFilter
            {
                FilterId = filterId,
                UserId = callerId,
                Name = "Team Current Optimization Report",
                Scope = "private",
                FilterConfig = new FilterConfig
                {
                    Teams = new List<int> { 10 },
                    TeamMembershipMode = "current"
                },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(report);

            var activeUser = new User { Id = Guid.NewGuid(), Name = "Seller 1", Email = "seller1@test.com" };
            activeUser.InternalId = 42;

            var team = new Team
            {
                Id = 10,
                Name = "GP Wealth group",
                UserTeams = new List<UserTeam>
                {
                    new UserTeam
                    {
                        TeamId = 10,
                        UserInternalId = 42,
                        User = activeUser,
                        StartDate = DateTime.UtcNow.AddMonths(-5),
                        EndDate = null
                    }
                }
            };

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team> { team });

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            var matchingContract = new Contract
            {
                ContractNumber = "CTR-OPT-1",
                UserInternalId = 42,
                TotalAmount = 5000m,
                ContractStatus = new ContractStatusEntity { Name = "Active" }
            };

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>(),
                It.Is<List<int>?>(ids => ids != null && ids.Contains(42)),
                true))
                .ReturnsAsync(new List<Contract> { matchingContract });

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Rows.Should().HaveCount(1);
            _contractRepositoryMock.Verify(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>(),
                It.Is<List<int>?>(ids => ids != null && ids.Contains(42)),
                true), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithHistoricalTeamFilter_ShouldPassTeamIdsToQuery()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "optim-filter-2";

            var report = new ReportFilter
            {
                FilterId = filterId,
                UserId = callerId,
                Name = "Team Historical Optimization Report",
                Scope = "private",
                FilterConfig = new FilterConfig
                {
                    Teams = new List<int> { 20 },
                    TeamMembershipMode = "historical"
                },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(report);

            var team = new Team
            {
                Id = 20,
                Name = "Historical Team",
                UserTeams = new List<UserTeam>()
            };

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team> { team });

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            _contractRepositoryMock.Setup(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.Is<List<int>?>(tids => tids != null && tids.Contains(20)),
                It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()))
                .ReturnsAsync(new List<Contract>());

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            _contractRepositoryMock.Verify(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.Is<List<int>?>(tids => tids != null && tids.Contains(20)),
                It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenActiveTeamHasNoMembers_ShouldReturnEmptyResponseEarlyWithoutQueryingContracts()
        {
            // Arrange
            var callerId = Guid.NewGuid().ToString();
            var filterId = "optim-filter-3";

            var report = new ReportFilter
            {
                FilterId = filterId,
                UserId = callerId,
                Name = "Empty Team Report",
                Scope = "private",
                FilterConfig = new FilterConfig
                {
                    Teams = new List<int> { 30 },
                    TeamMembershipMode = "current"
                },
                OutputColumns = new List<OutputColumn>
                {
                    new OutputColumn { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 }
                }
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(callerId, filterId))
                .ReturnsAsync(report);

            var team = new Team
            {
                Id = 30,
                Name = "Team With No Members",
                UserTeams = new List<UserTeam>() // no members!
            };

            _teamRepositoryMock.Setup(t => t.GetAllAsync(It.IsAny<HashSet<int>?>()))
                .ReturnsAsync(new List<Team> { team });

            _classificationLevelRepositoryMock.Setup(c => c.GetAllAsync())
                .ReturnsAsync(new List<ClassificationLevel>());

            // Act
            var result = await _service.ExecuteAsync(callerId, filterId, null, 1, 25);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.TotalCount.Should().Be(0);
            result.Data!.Rows.Should().BeEmpty();

            // Contract repo should NOT have been called at all!
            _contractRepositoryMock.Verify(c => c.GetAllAsync(
                It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<List<string>?>(), It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(), It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(),
                It.IsAny<List<string>?>(), It.IsAny<bool>(), It.IsAny<bool?>()), Times.Never);
        }
    }
}
