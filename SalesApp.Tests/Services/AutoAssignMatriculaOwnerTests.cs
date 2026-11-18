using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class AutoAssignMatriculaOwnerTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly Mock<IUserMatriculaRepository> _mockUserMatriculaRepository;
        private readonly Mock<IMatriculaRepository> _mockMatriculaRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IContractMetadataRepository> _mockMetadataRepository;
        private readonly Mock<IPVRepository> _mockPvRepository;
        private readonly Mock<IContractStatusMapper> _mockStatusMapper;
        private readonly Mock<IContractStatusService> _mockStatusService;
        private readonly Mock<IImportErrorService> _mockErrorService;
        private readonly Mock<IPendingClaimService> _mockPendingClaimService;
        private readonly ImportExecutionService _service;

        private readonly Dictionary<string, string> _mappings = new()
        {
            { "Cota", "ContractNumber" },
            { "Matricula", "MatriculaNumber" },
            { "Crédito Venda", "TotalAmount" },
            { "Dt Venda", "SaleStartDate" },
            { "Situação Cobrança", "Status" },
            { "Grupo", "GroupId" },
            { "Consorciado", "CustomerName" },
            { "NumCota", "Quota" }
        };

        public AutoAssignMatriculaOwnerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);

            _mockContractRepository = new Mock<IContractRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockRoleRepository = new Mock<IRoleRepository>();
            _mockUserMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _mockMatriculaRepository = new Mock<IMatriculaRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockMetadataRepository = new Mock<IContractMetadataRepository>();
            _mockPvRepository = new Mock<IPVRepository>();
            _mockStatusMapper = new Mock<IContractStatusMapper>();
            _mockStatusService = new Mock<IContractStatusService>();
            _mockErrorService = new Mock<IImportErrorService>();
            _mockPendingClaimService = new Mock<IPendingClaimService>();

            _mockStatusService.Setup(s => s.GetStatusIdByNameAsync(It.IsAny<string>())).ReturnsAsync(1);
            _mockStatusMapper.Setup(m => m.MapStatus(It.IsAny<string>())).Returns("Active");
            _mockGroupRepository.Setup(g => g.GetByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(new Group { Id = 1, Name = "001001", IsActive = true });

            _service = new ImportExecutionService(
                _mockContractRepository.Object,
                _mockGroupRepository.Object,
                _mockUserRepository.Object,
                _mockRoleRepository.Object,
                _mockUserMatriculaRepository.Object,
                _mockMatriculaRepository.Object,
                _mockEmailService.Object,
                _context,
                _mockMetadataRepository.Object,
                _mockPvRepository.Object,
                _mockStatusMapper.Object,
                _mockStatusService.Object,
                _mockErrorService.Object,
                _mockPendingClaimService.Object
            );
        }

        [Fact]
        public async Task ExecuteContractDashboardImportAsync_WhenNewContract_AndOwnerHasFlagTrue_ShouldAssignOwner()
        {
            // Arrange
            var matriculaNumber = "3650";
            var ownerUser = new User
            {
                Id = Guid.NewGuid(),
                InternalId = 42,
                Name = "Matricula Owner",
                Email = "owner@test.com",
                AutoAssignNewContractsToMatriculaOwner = true,
                IsActive = true
            };

            var matricula = new Matricula
            {
                Id = 100,
                MatriculaNumber = matriculaNumber,
                Status = "active"
            };

            _mockMatriculaRepository.Setup(r => r.GetByMatriculaNumberAsync(matriculaNumber))
                .ReturnsAsync(matricula);

            _mockUserMatriculaRepository.Setup(r => r.GetOwnerByMatriculaIdAsync(matricula.Id))
                .ReturnsAsync(new UserMatricula
                {
                    Id = 1,
                    MatriculaId = matricula.Id,
                    UserInternalId = ownerUser.InternalId,
                    IsOwner = true,
                    IsActive = true,
                    User = ownerUser
                });

            _mockContractRepository.Setup(r => r.GetByContractNumbersAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<Contract>());

            List<Contract>? createdContracts = null;
            _mockContractRepository.Setup(r => r.CreateBatchAsync(It.IsAny<List<Contract>>()))
                .Callback<List<Contract>>(list => createdContracts = list)
                .ReturnsAsync((List<Contract> list) => list);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "1100152518" },
                    { "Matricula", matriculaNumber },
                    { "Crédito Venda", "100.000,00" },
                    { "Dt Venda", "2026-01-13" },
                    { "Situação Cobrança", "Normal" },
                    { "Grupo", "001001" },
                    { "Consorciado", "CLIENTE TESTE" },
                    { "NumCota", "4225" }
                }
            };

            // Act
            var result = await _service.ExecuteContractDashboardImportAsync(
                uploadId: "pbi-scrape-test",
                importSessionId: 1,
                rows: rows,
                mappings: _mappings,
                allowAutoCreateGroups: true
            );

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            createdContracts.Should().NotBeNull();
            createdContracts!.Should().HaveCount(1);
            createdContracts![0].UserInternalId.Should().Be(42, "the owner had AutoAssignNewContractsToMatriculaOwner = true");
        }

        [Fact]
        public async Task ExecuteContractDashboardImportAsync_WhenNewContract_AndOwnerHasFlagFalse_ShouldNotAssignOwner()
        {
            // Arrange
            var matriculaNumber = "3650";
            var ownerUser = new User
            {
                Id = Guid.NewGuid(),
                InternalId = 42,
                Name = "Matricula Owner",
                Email = "owner@test.com",
                AutoAssignNewContractsToMatriculaOwner = false, // Flag disabled
                IsActive = true
            };

            var matricula = new Matricula
            {
                Id = 100,
                MatriculaNumber = matriculaNumber,
                Status = "active"
            };

            _mockMatriculaRepository.Setup(r => r.GetByMatriculaNumberAsync(matriculaNumber))
                .ReturnsAsync(matricula);

            _mockUserMatriculaRepository.Setup(r => r.GetOwnerByMatriculaIdAsync(matricula.Id))
                .ReturnsAsync(new UserMatricula
                {
                    Id = 1,
                    MatriculaId = matricula.Id,
                    UserInternalId = ownerUser.InternalId,
                    IsOwner = true,
                    IsActive = true,
                    User = ownerUser
                });

            _mockContractRepository.Setup(r => r.GetByContractNumbersAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<Contract>());

            List<Contract>? createdContracts = null;
            _mockContractRepository.Setup(r => r.CreateBatchAsync(It.IsAny<List<Contract>>()))
                .Callback<List<Contract>>(list => createdContracts = list)
                .ReturnsAsync((List<Contract> list) => list);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "1100152518" },
                    { "Matricula", matriculaNumber },
                    { "Crédito Venda", "100.000,00" },
                    { "Dt Venda", "2026-01-13" },
                    { "Situação Cobrança", "Normal" },
                    { "Grupo", "001001" },
                    { "Consorciado", "CLIENTE TESTE" },
                    { "NumCota", "4225" }
                }
            };

            // Act
            var result = await _service.ExecuteContractDashboardImportAsync(
                uploadId: "pbi-scrape-test",
                importSessionId: 1,
                rows: rows,
                mappings: _mappings,
                allowAutoCreateGroups: true
            );

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            createdContracts.Should().NotBeNull();
            createdContracts!.Should().HaveCount(1);
            createdContracts![0].UserInternalId.Should().BeNull("the owner had AutoAssignNewContractsToMatriculaOwner = false");
        }

        [Fact]
        public async Task ExecuteContractDashboardImportAsync_WhenExistingContractWithoutOwner_ShouldRemainUntouched()
        {
            // Arrange
            var matriculaNumber = "3650";
            var ownerUser = new User
            {
                Id = Guid.NewGuid(),
                InternalId = 42,
                Name = "Matricula Owner",
                Email = "owner@test.com",
                AutoAssignNewContractsToMatriculaOwner = true,
                IsActive = true
            };

            var matricula = new Matricula
            {
                Id = 100,
                MatriculaNumber = matriculaNumber,
                Status = "active"
            };

            _mockMatriculaRepository.Setup(r => r.GetByMatriculaNumberAsync(matriculaNumber))
                .ReturnsAsync(matricula);

            _mockUserMatriculaRepository.Setup(r => r.GetOwnerByMatriculaIdAsync(matricula.Id))
                .ReturnsAsync(new UserMatricula
                {
                    Id = 1,
                    MatriculaId = matricula.Id,
                    UserInternalId = ownerUser.InternalId,
                    IsOwner = true,
                    IsActive = true,
                    User = ownerUser
                });

            // Existing contract in DB without user assigned
            var existingContract = new Contract
            {
                Id = 555,
                ContractNumber = "1100152518",
                UserInternalId = null,
                TotalAmount = 80000,
                IsActive = true
            };

            _mockContractRepository.Setup(r => r.GetByContractNumbersAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<Contract> { existingContract });

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "1100152518" },
                    { "Matricula", matriculaNumber },
                    { "Crédito Venda", "100.000,00" },
                    { "Dt Venda", "2026-01-13" },
                    { "Situação Cobrança", "Normal" },
                    { "Grupo", "001001" },
                    { "Consorciado", "CLIENTE TESTE" },
                    { "NumCota", "4225" }
                }
            };

            // Act
            var result = await _service.ExecuteContractDashboardImportAsync(
                uploadId: "pbi-scrape-test",
                importSessionId: 1,
                rows: rows,
                mappings: _mappings,
                allowAutoCreateGroups: true
            );

            // Assert
            result.ProcessedRows.Should().Be(1);
            existingContract.UserInternalId.Should().BeNull("existing contracts must remain untouched during Ava Pro import");
        }
    }
}
