using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using System.Security.Claims;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests
{
    public class ContractsControllerTests
    {
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IContractAggregationService> _mockAggregationService;
        private readonly Mock<IUserMatriculaRepository> _mockMatriculaRepository;
        private readonly Mock<IMatriculaRepository> _mockBaseMatriculaRepository;
        private readonly Mock<IMessageService> _mockMessageService;
        private readonly Mock<IUserScopeService> _mockUserScopeService;
        private readonly Mock<IExportService> _mockExportService;
        private readonly Mock<IContractStatusMapper> _mockStatusMapper;
        private readonly Mock<IContractStatusService> _mockStatusService;
        private readonly Mock<IPendingContractClaimRepository> _mockPendingClaimRepository;
        private readonly ContractsController _controller;

        public ContractsControllerTests()
        {
            _mockContractRepository = new Mock<IContractRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockAggregationService = new Mock<IContractAggregationService>();
            _mockMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _mockBaseMatriculaRepository = new Mock<IMatriculaRepository>();
            _mockMessageService = new Mock<IMessageService>();
            _mockUserScopeService = new Mock<IUserScopeService>();
            _mockExportService = new Mock<IExportService>();
            _mockStatusMapper = new Mock<IContractStatusMapper>();
            _mockStatusService = new Mock<IContractStatusService>();
            _mockPendingClaimRepository = new Mock<IPendingContractClaimRepository>();

            _mockStatusMapper.Setup(s => s.IsValidStatus(It.IsAny<string>())).Returns(true);
            _mockStatusMapper.Setup(s => s.GetValidStatuses()).Returns(new string[] { "Active", "Inactive" });

            _controller = new ContractsController(
                _mockContractRepository.Object, 
                _mockUserRepository.Object, 
                _mockGroupRepository.Object, 
                _mockAggregationService.Object, 
                _mockMatriculaRepository.Object, 
                _mockBaseMatriculaRepository.Object,
                _mockMessageService.Object,
                _mockUserScopeService.Object,
                _mockExportService.Object,
                _mockStatusMapper.Object,
                _mockStatusService.Object,
                _mockPendingClaimRepository.Object);
            
            // Setup MessageService to return English messages for tests
            var enumToMessage = new System.Func<AppMessage, string>(msg => {
                var text = System.Text.RegularExpressions.Regex.Replace(msg.ToString(), "([a-z])([A-Z])", "$1 $2");
                return char.ToUpper(text[0]) + text.Substring(1).ToLower();
            });
            _mockMessageService.Setup(m => m.Get(It.IsAny<AppMessage>())).Returns(enumToMessage);
            
            // Setup admin user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task CreateContract_WithUniqueContractNumber_ShouldSucceed()
        {
            // Arrange
            var request = new ContractRequest
            {
                ContractNumber = "1100000999",
                UserId = Guid.NewGuid(),
                GroupId = 1,
                TotalAmount = 100.00m,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var user = new User { Id = request.UserId!.Value, IsActive = true };
            var group = new Group { Id = request.GroupId!.Value, IsActive = true };
            var contract = new Contract { Id = 1, ContractNumber = request.ContractNumber };

            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync((Contract?)null);
            _mockUserRepository.Setup(x => x.GetByIdAsync(request.UserId!.Value))
                .ReturnsAsync(user);
            _mockGroupRepository.Setup(x => x.GetByIdAsync(request.GroupId!.Value))
                .ReturnsAsync(group);
            _mockContractRepository.Setup(x => x.CreateAsync(It.IsAny<Contract>()))
                .ReturnsAsync(contract);

            // Act
            var result = await _controller.CreateContract(request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeTrue();
            response.Message.Should().Be("Contract created successfully");
        }

        [Fact]
        public async Task CreateContract_WithDuplicateContractNumber_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ContractRequest
            {
                ContractNumber = "1100000921", // Existing contract number
                UserId = Guid.NewGuid(),
                GroupId = 1,
                TotalAmount = 100.00m,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var existingContract = new Contract { Id = 1, ContractNumber = request.ContractNumber };

            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync(existingContract);

            // Act
            var result = await _controller.CreateContract(request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Contract number already exists");
        }

        [Fact]
        public async Task UpdateContract_WithUniqueContractNumber_ShouldSucceed()
        {
            // Arrange
            var contractId = 1;
            var request = new UpdateContractRequest
            {
                ContractNumber = "1100000999"
            };

            var existingContract = new Contract { Id = contractId, ContractNumber = "1100000921" };
            var updatedContract = new Contract { Id = contractId, ContractNumber = request.ContractNumber };

            _mockContractRepository.Setup(x => x.GetByIdAsync(contractId))
                .ReturnsAsync(existingContract);
            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync((Contract?)null);
            _mockContractRepository.Setup(x => x.UpdateAsync(It.IsAny<Contract>()))
                .ReturnsAsync(updatedContract);

            // Act
            var result = await _controller.UpdateContract(contractId, request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeTrue();
            response.Message.Should().Be("Contract updated successfully");
        }

        [Fact]
        public async Task UpdateContract_WithDuplicateContractNumber_ShouldReturnBadRequest()
        {
            // Arrange
            var contractId = 1;
            var request = new UpdateContractRequest
            {
                ContractNumber = "1100000922" // Existing contract number from different contract
            };

            var existingContract = new Contract { Id = contractId, ContractNumber = "1100000921" };
            var duplicateContract = new Contract { Id = 2, ContractNumber = "1100000922" };

            _mockContractRepository.Setup(x => x.GetByIdAsync(contractId))
                .ReturnsAsync(existingContract);
            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync(duplicateContract);

            // Act
            var result = await _controller.UpdateContract(contractId, request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Contract number already exists");
        }

        [Fact]
        public async Task UpdateContract_WithSameContractNumber_ShouldSucceed()
        {
            // Arrange
            var contractId = 1;
            var request = new UpdateContractRequest
            {
                ContractNumber = "1100000921" // Same contract number as current
            };

            var existingContract = new Contract { Id = contractId, ContractNumber = "1100000921" };

            _mockContractRepository.Setup(x => x.GetByIdAsync(contractId))
                .ReturnsAsync(existingContract);
            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync(existingContract); // Returns the same contract
            _mockContractRepository.Setup(x => x.UpdateAsync(It.IsAny<Contract>()))
                .ReturnsAsync(existingContract);

            // Act
            var result = await _controller.UpdateContract(contractId, request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeTrue();
            response.Message.Should().Be("Contract updated successfully");
        }

        [Fact]
        public async Task CreateContract_WithoutUserId_ShouldSucceed()
        {
            // Arrange
            var request = new ContractRequest
            {
                ContractNumber = "1100000950",
                UserId = null, // No user assigned
                GroupId = 1,
                TotalAmount = 150.00m,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var group = new Group { Id = request.GroupId!.Value, IsActive = true };
            var contract = new Contract { Id = 1, ContractNumber = request.ContractNumber, UserInternalId = null };

            _mockContractRepository.Setup(x => x.GetByContractNumberAsync(request.ContractNumber))
                .ReturnsAsync((Contract?)null);
            _mockGroupRepository.Setup(x => x.GetByIdAsync(request.GroupId!.Value))
                .ReturnsAsync(group);
            _mockContractRepository.Setup(x => x.CreateAsync(It.IsAny<Contract>()))
                .ReturnsAsync(contract);

            // Act
            var result = await _controller.CreateContract(request);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeTrue();
            response.Message.Should().Be("Contract created successfully");
            
            // Verify user repository was never called since UserId was null
            _mockUserRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UpdateContract_WithNullUserId_ShouldClearUserInternalIdAndMatricula()
        {
            // Arrange
            var contractId = 1;
            var existingContract = new Contract
            {
                Id = contractId,
                ContractNumber = "1100000921",
                UserInternalId = 10,
                MatriculaId = 5,
                TempMatricula = "MATR-123",
                IsActive = true
            };

            var updateRequest = new UpdateContractRequest
            {
                UserId = null,
                MatriculaNumber = null
            };

            Contract? updatedModelPassedToRepo = null;

            _mockContractRepository.Setup(x => x.GetByIdAsync(contractId))
                .ReturnsAsync(existingContract);
            _mockContractRepository.Setup(x => x.UpdateAsync(It.IsAny<Contract>()))
                .Callback<Contract>(c => updatedModelPassedToRepo = c)
                .ReturnsAsync(existingContract);

            // Act
            var result = await _controller.UpdateContract(contractId, updateRequest);

            // Assert
            result.Should().BeOfType<ActionResult<ApiResponse<ContractResponse>>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<ContractResponse>>().Subject;
            response.Success.Should().BeTrue();

            updatedModelPassedToRepo.Should().NotBeNull();
            updatedModelPassedToRepo!.UserInternalId.Should().BeNull();
            updatedModelPassedToRepo!.MatriculaId.Should().BeNull();
            updatedModelPassedToRepo!.TempMatricula.Should().BeNull();
        }

        [Fact]
        public async Task GetContracts_MapsHasPaymentAndCalculatesIsAwaitingPayment()
        {
            // Arrange
            var activeStatus = new ContractStatusEntity { Id = 1, Name = "Active" };
            var lateStatus = new ContractStatusEntity { Id = 2, Name = "Late1" };

            var contracts = new List<Contract>
            {
                new Contract { Id = 1, ContractNumber = "CTR-1", ContractStatus = activeStatus, HasPayment = false, IsActive = true },
                new Contract { Id = 2, ContractNumber = "CTR-2", ContractStatus = activeStatus, HasPayment = true, IsActive = true },
                new Contract { Id = 3, ContractNumber = "CTR-3", ContractStatus = lateStatus, HasPayment = false, IsActive = true },
                new Contract { Id = 4, ContractNumber = "CTR-4", ContractStatus = activeStatus, HasPayment = null, IsActive = true }
            };

            _mockUserScopeService.Setup(s => s.GetContractScopeAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(new UserScopeContext { IsGlobal = true });

            _mockContractRepository.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
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
                .ReturnsAsync((contracts, 4));

            _mockContractRepository.Setup(r => r.GetAggregationAsync(
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
                .ReturnsAsync(new ContractAggregation());

            // Act
            var result = await _controller.GetContracts(page: 1, pageSize: 10, awaitingPayment: true);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>().Subject;
            var items = response.Data!.Items;

            // Item 1: Active + HasPayment false -> IsAwaitingPayment = true
            items[0].HasPayment.Should().Be(false);
            items[0].IsAwaitingPayment.Should().BeTrue();

            // Item 2: Active + HasPayment true -> IsAwaitingPayment = false
            items[1].HasPayment.Should().Be(true);
            items[1].IsAwaitingPayment.Should().BeFalse();

            // Item 3: Late1 + HasPayment false -> IsAwaitingPayment = false
            items[2].HasPayment.Should().Be(false);
            items[2].IsAwaitingPayment.Should().BeFalse();

            // Item 4: Active + HasPayment null -> IsAwaitingPayment = false
            items[3].HasPayment.Should().BeNull();
            items[3].IsAwaitingPayment.Should().BeFalse();
        }
    }
}