using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
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
        private readonly ContractsController _controller;

        public ContractsControllerTests()
        {
            _mockContractRepository = new Mock<IContractRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _controller = new ContractsController(_mockContractRepository.Object, _mockUserRepository.Object, _mockGroupRepository.Object);
            
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
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddDays(30)
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
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddDays(30)
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
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddDays(30)
            };

            var group = new Group { Id = request.GroupId!.Value, IsActive = true };
            var contract = new Contract { Id = 1, ContractNumber = request.ContractNumber, UserId = null };

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
    }
}