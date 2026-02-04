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
        private readonly Mock<IMessageService> _mockMessageService;
        private readonly ContractsController _controller;

        public HistoricProductionTests()
        {
            _mockRepository = new Mock<IContractRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockGroupRepository = new Mock<IGroupRepository>();
            _mockAggregationService = new Mock<IContractAggregationService>();
            _mockMatriculaRepository = new Mock<IUserMatriculaRepository>();
            _mockMessageService = new Mock<IMessageService>();
            _controller = new ContractsController(
                _mockRepository.Object,
                _mockUserRepository.Object,
                _mockGroupRepository.Object,
                _mockAggregationService.Object,
                _mockMatriculaRepository.Object,
                _mockMessageService.Object
            );

            // Setup MessageService to return English messages for tests
            var enumToMessage = new System.Func<AppMessage, string>(msg => {
                var text = System.Text.RegularExpressions.Regex.Replace(msg.ToString(), "([a-z])([A-Z])", "$1 $2");
                return char.ToUpper(text[0]) + text.Substring(1).ToLower();
            });
            _mockMessageService.Setup(m => m.Get(It.IsAny<AppMessage>())).Returns(enumToMessage);

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
            var monthlyData = new List<MonthlyProduction>
            {
                new MonthlyProduction { Period = "2024-07", TotalProduction = 300000, ContractCount = 3 }
            };

            _mockRepository.Setup(r => r.GetMonthlyProductionAsync(null, null, null, null))
                .ReturnsAsync(monthlyData);

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Success.Should().BeTrue();
            response.Data!.MonthlyData.Should().HaveCount(1);
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
            var monthlyData = new List<MonthlyProduction>
            {
                new MonthlyProduction { Period = "2024-07", TotalProduction = 250000, ContractCount = 2 },
                new MonthlyProduction { Period = "2024-08", TotalProduction = 100000, ContractCount = 1 },
                new MonthlyProduction { Period = "2024-09", TotalProduction = 150000, ContractCount = 1 }
            };

            _mockRepository.Setup(r => r.GetMonthlyProductionAsync(null, null, null, null))
                .ReturnsAsync(monthlyData);

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Data!.MonthlyData.Should().HaveCount(3);
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
            _mockRepository.Setup(r => r.GetMonthlyProductionAsync(null, null, null, null))
                .ReturnsAsync(new List<MonthlyProduction>());

            // Act
            var result = await _controller.GetHistoricProduction();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<HistoricProductionResponse>>().Subject;

            response.Success.Should().BeTrue();
            response.Data!.MonthlyData.Should().BeEmpty();
            response.Data.TotalProduction.Should().Be(0);
            response.Data.TotalContracts.Should().Be(0);
        }

        [Fact]
        public async Task GetHistoricProduction_WithDateFilter_PassesCorrectParameters()
        {
            // Arrange
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            
            _mockRepository.Setup(r => r.GetMonthlyProductionAsync(null, startDate, endDate, null))
                .ReturnsAsync(new List<MonthlyProduction>());

            // Act
            await _controller.GetHistoricProduction(startDate, endDate);

            // Assert
            _mockRepository.Verify(r => r.GetMonthlyProductionAsync(null, startDate, endDate, null), Times.Once);
        }

        [Fact]
        public async Task GetHistoricProduction_WithUserId_PassesCorrectParameters()
        {
            // Arrange
            var userId = Guid.NewGuid();
            
            _mockRepository.Setup(r => r.GetMonthlyProductionAsync(userId, null, null, null))
                .ReturnsAsync(new List<MonthlyProduction>());

            // Act
            await _controller.GetHistoricProduction(null, null, userId);

            // Assert
            _mockRepository.Verify(r => r.GetMonthlyProductionAsync(userId, null, null, null), Times.Once);
        }
    }
}
