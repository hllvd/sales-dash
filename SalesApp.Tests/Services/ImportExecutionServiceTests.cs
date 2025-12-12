using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ImportExecutionServiceTests
    {
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly ImportExecutionService _service;

        public ImportExecutionServiceTests()
        {
            _mockContractRepository = new Mock<IContractRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockRoleRepository = new Mock<IRoleRepository>();

            _service = new ImportExecutionService(
                _mockContractRepository.Object,
                _mockGroupRepository.Object,
                _mockUserRepository.Object,
                _mockRoleRepository.Object
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

            Contract? capturedContract = null;
            _mockContractRepository.Setup(r => r.CreateAsync(It.IsAny<Contract>()))
                .Callback<Contract>(c => capturedContract = c)
                .ReturnsAsync((Contract c) => c);

            // Act
            var result = await _service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            
            capturedContract.Should().NotBeNull();
            capturedContract!.ContractType.Should().Be(1);
            capturedContract.Quota.Should().Be(10);
            capturedContract.PvId.Should().Be(5);
            capturedContract.CustomerName.Should().Be("John Doe");
        }
    }
}
