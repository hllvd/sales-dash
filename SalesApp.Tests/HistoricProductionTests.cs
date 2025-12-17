using Xunit;
using FluentAssertions;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace SalesApp.Tests
{
    public class HistoricProductionTests
    {
        private readonly Mock<IContractRepository> _mockRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGroupRepository> _mockGroupRepository;
        private readonly Mock<IContractAggregationService> _mockAggregationService;
        private readonly Mock<IUserMatriculaRepository> _mockMatriculaRepository;
        private readonly ContractsController _controller;

        public HistoricProductionTests()
        {
            _mockRepository = new Mock<IContractRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockAggregationService = new Mock<IContractAggregationService>();
            _mockMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _controller = new ContractsController(
                _mockRepository.Object,
                _mockUserRepository.Object,
                _mockGroupRepository.Object,
                _mockAggregationService.Object,
                _mockMatriculaRepository.Object
            );

            // Setup user context
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin")
            }));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task GetHistoricProduction_SingleMonth_ReturnsCorrectData()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { Id = 1, TotalAmount = 100000, SaleStartDate = new DateTime(2024, 7, 1) },
                new Contract { Id = 2, TotalAmount = 150000, SaleStartDate = new DateTime(2024, 7, 15) },
                new Contract { Id = 3, TotalAmount = 50000, SaleStartDate = new DateTime(2024, 7, 30) }
            };

            _mockRepository.Setup(r => r.GetAllAsync(null, null, null, null))
                .ReturnsAsync(contracts);

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Success.Should().BeTrue();
            response.Data.MonthlyData.Should().HaveCount(1);
            response.Data.MonthlyData[0].Period.Should().Be("2024-07");
            response.Data.MonthlyData[0].TotalProduction.Should().Be(300000);
            response.Data.MonthlyData[0].ContractCount.Should().Be(3);
            response.Data.TotalProduction.Should().Be(300000);
            response.Data.TotalContracts.Should().Be(3);
        }

        [Fact]
        public async Task GetHistoricProduction_MultipleMonths_ReturnsSortedData()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { Id = 1, TotalAmount = 100000, SaleStartDate = new DateTime(2024, 8, 1) },
                new Contract { Id = 2, TotalAmount = 200000, SaleStartDate = new DateTime(2024, 7, 1) },
                new Contract { Id = 3, TotalAmount = 150000, SaleStartDate = new DateTime(2024, 9, 1) },
                new Contract { Id = 4, TotalAmount = 50000, SaleStartDate = new DateTime(2024, 7, 15) }
            };

            _mockRepository.Setup(r => r.GetAllAsync(null, null, null, null))
                .ReturnsAsync(contracts);

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Data.MonthlyData.Should().HaveCount(3);
            response.Data.MonthlyData[0].Period.Should().Be("2024-07");
            response.Data.MonthlyData[0].TotalProduction.Should().Be(250000);
            response.Data.MonthlyData[0].ContractCount.Should().Be(2);
            
            response.Data.MonthlyData[1].Period.Should().Be("2024-08");
            response.Data.MonthlyData[1].TotalProduction.Should().Be(100000);
            response.Data.MonthlyData[1].ContractCount.Should().Be(1);
            
            response.Data.MonthlyData[2].Period.Should().Be("2024-09");
            response.Data.MonthlyData[2].TotalProduction.Should().Be(150000);
            response.Data.MonthlyData[2].ContractCount.Should().Be(1);

            response.Data.TotalProduction.Should().Be(500000);
            response.Data.TotalContracts.Should().Be(4);
        }

        [Fact]
        public async Task GetHistoricProduction_NoContracts_ReturnsEmptyData()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllAsync(null, null, null, null))
                .ReturnsAsync(new List<Contract>());

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Success.Should().BeTrue();
            response.Data.MonthlyData.Should().BeEmpty();
            response.Data.TotalProduction.Should().Be(0);
            response.Data.TotalContracts.Should().Be(0);
        }

        [Fact]
        public async Task GetHistoricProduction_WithDateFilter_PassesCorrectParameters()
        {
            // Arrange
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            
            _mockRepository.Setup(r => r.GetAllAsync(null, null, startDate, endDate))
                .ReturnsAsync(new List<Contract>());

            // Act
            await _controller.GetHistoricProduction(startDate, endDate);

            // Assert
            _mockRepository.Verify(r => r.GetAllAsync(null, null, startDate, endDate), Times.Once);
        }

        [Fact]
        public async Task GetHistoricProduction_WithUserId_PassesCorrectParameters()
        {
            // Arrange
            var userId = Guid.NewGuid();
            
            _mockRepository.Setup(r => r.GetAllAsync(userId, null, null, null))
                .ReturnsAsync(new List<Contract>());

            // Act
            await _controller.GetHistoricProduction(null, null, userId);

            // Assert
            _mockRepository.Verify(r => r.GetAllAsync(userId, null, null, null), Times.Once);
        }
    }
}
