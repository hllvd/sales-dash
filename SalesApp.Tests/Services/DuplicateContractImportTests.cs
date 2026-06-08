using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class DuplicateContractImportTests
    {
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly Mock<IUserMatriculaRepository> _mockMatriculaRepository;
        private readonly Mock<IMatriculaRepository> _mockBaseMatriculaRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<AppDbContext> _mockContext;
        private readonly Mock<IContractMetadataRepository> _mockMetadataRepository;
        private readonly Mock<IPVRepository> _mockPvRepository;
        private readonly Mock<IContractStatusMapper> _mockStatusMapper;
        private readonly Mock<IContractStatusService> _mockStatusService;
        private readonly Mock<IImportErrorService> _mockErrorService;
        private readonly Mock<IPendingClaimService> _mockPendingClaimService;
        private readonly ImportExecutionService _service;

        public DuplicateContractImportTests()
        {
            _mockContractRepository = new Mock<IContractRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockRoleRepository = new Mock<IRoleRepository>();
            _mockMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _mockBaseMatriculaRepository = new Mock<IMatriculaRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>(), new Mock<IHttpContextAccessor>().Object);
            _mockMetadataRepository = new Mock<IContractMetadataRepository>();
            _mockPvRepository = new Mock<IPVRepository>();
            _mockStatusMapper = new Mock<IContractStatusMapper>();
            _mockStatusService = new Mock<IContractStatusService>();
            _mockErrorService = new Mock<IImportErrorService>();
            _mockPendingClaimService = new Mock<IPendingClaimService>();

            _mockStatusService.Setup(s => s.GetStatusIdByNameAsync(It.IsAny<string>())).ReturnsAsync(1);
            
            _service = new ImportExecutionService(
                _mockContractRepository.Object,
                _mockGroupRepository.Object,
                _mockUserRepository.Object,
                _mockRoleRepository.Object,
                _mockMatriculaRepository.Object,
                _mockBaseMatriculaRepository.Object,
                _mockEmailService.Object,
                _mockContext.Object,
                _mockMetadataRepository.Object,
                _mockPvRepository.Object,
                _mockStatusMapper.Object,
                _mockStatusService.Object,
                _mockErrorService.Object,
                _mockPendingClaimService.Object
            );

            _mockStatusMapper.Setup(m => m.MapStatus(It.IsAny<string>())).Returns((string s) => s);
        }

        [Fact]
        public async Task ExecuteContractImportAsync_ShouldHandleDuplicatesInSameBatch()
        {
            // Arrange
            var uploadId = "duplicate-test";
            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "contract", "DUP-001" },
                    { "email", "user@test.com" },
                    { "amount", "100,00" },
                    { "date", "2024-01-01" },
                    { "customer", "Original Customer" }
                },
                new Dictionary<string, string>
                {
                    { "contract", "DUP-001" },
                    { "email", "user@test.com" },
                    { "amount", "200,00" },
                    { "date", "2024-01-02" },
                    { "customer", "Updated Customer" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "contract", "ContractNumber" },
                { "email", "UserEmail" },
                { "amount", "TotalAmount" },
                { "date", "SaleStartDate" },
                { "customer", "CustomerName" }
            };

            _mockContractRepository.Setup(r => r.GetByContractNumbersAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<Contract>()); // DB is empty

            _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), IsActive = true });

            List<Contract>? capturedContracts = null;
            _mockContractRepository.Setup(r => r.CreateBatchAsync(It.IsAny<List<Contract>>()))
                .Callback<List<Contract>>(contracts => capturedContracts = contracts)
                .ReturnsAsync((List<Contract> c) => c);

            // Act
            var result = await _service.ExecuteContractImportAsync(uploadId, 1, rows, mappings, "yyyy-MM-dd");

            // Assert
            result.ProcessedRows.Should().Be(2); // Both rows processed
            result.FailedRows.Should().Be(0);
            
            capturedContracts.Should().NotBeNull();
            capturedContracts.Should().HaveCount(1, "because duplicates in the same batch should be merged");
            capturedContracts![0].ContractNumber.Should().Be("DUP-001");
            capturedContracts![0].TotalAmount.Should().Be(200, "because the second row should win (upsert behavior)");
            capturedContracts![0].CustomerName.Should().Be("Updated Customer");
        }
    }
}
