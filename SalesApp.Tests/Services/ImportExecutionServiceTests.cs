using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ImportExecutionServiceTests
    {
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly Mock<IUserMatriculaRepository> _mockMatriculaRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<AppDbContext> _mockContext;
        private readonly ImportExecutionService _service;

        public ImportExecutionServiceTests()
        {
            _mockContractRepository = new Mock<IContractRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockRoleRepository = new Mock<IRoleRepository>();
            _mockMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            
            _service = new ImportExecutionService(
                _mockContractRepository.Object,
                _mockGroupRepository.Object,
                _mockUserRepository.Object,
                _mockRoleRepository.Object,
                _mockMatriculaRepository.Object,
                _mockEmailService.Object,
                _mockContext.Object
            );
        }

        [Fact]
        public async Task ExecuteContractImportAsync_ShouldMapContractTypeAndQuotaAndPvIdAndCustomerName()
        {
            // Arrange
            var uploadId = "test-upload-id";
            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "contract_num", "CTR-001" },
                    { "user_email", "test@test.com" },
                    { "amount", "1000" },
                    { "group_id", "1" },
                    { "type", "1" },
                    { "quota_val", "10" },
                    { "pv_id", "5" },
                    { "cust_name", "John Doe" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "contract_num", "ContractNumber" },
                { "user_email", "UserEmail" },
                { "amount", "TotalAmount" },
                { "group_id", "GroupId" },
                { "type", "ContractType" },
                { "quota_val", "Quota" },
                { "pv_id", "PvId" },
                { "cust_name", "CustomerName" }
            };

            _mockGroupRepository.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(new Group { Id = 1, IsActive = true });

            _mockUserRepository.Setup(r => r.GetByEmailAsync("test@test.com"))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), IsActive = true });

            List<Contract>? capturedContracts = null;
            _mockContractRepository.Setup(r => r.CreateBatchAsync(It.IsAny<List<Contract>>()))
                .Callback<List<Contract>>(contracts => capturedContracts = contracts)
                .ReturnsAsync((List<Contract> contracts) => contracts);

            // Act
            var result = await _service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            
            capturedContracts.Should().NotBeNull();
            capturedContracts.Should().HaveCount(1);
            var capturedContract = capturedContracts![0];
            capturedContract.ContractType.Should().Be(1);
            capturedContract.Quota.Should().Be(10);
            capturedContract.PvId.Should().Be(5);
            capturedContract.CustomerName.Should().Be("John Doe");
        }
    }
}
