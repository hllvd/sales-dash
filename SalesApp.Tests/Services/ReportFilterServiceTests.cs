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

            _teamRepositoryMock.Setup(t => t.GetAllAsync())
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
                It.IsAny<List<Guid>?>()))
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
                It.IsAny<List<Guid>?>()), Times.Once);
        }
    }
}
