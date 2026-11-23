using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests
{
    public class MonitoringControllerTests
    {
        private readonly Mock<IMonitoringService> _mockMonitoringService;
        private readonly MonitoringController _controller;

        public MonitoringControllerTests()
        {
            _mockMonitoringService = new Mock<IMonitoringService>();
            _controller = new MonitoringController(_mockMonitoringService.Object);
        }

        [Fact]
        public async Task GetContractUpdateSummary_ShouldReturnSummaryResponse()
        {
            // Arrange
            var expectedSummary = new ContractUpdateSummaryResponse
            {
                LessThan1Hour = 5,
                LessThan3Hours = 10,
                LessThan6Hours = 15,
                LessThan1Day = 25,
                LessThan3Days = 40,
                LessThan1Week = 60,
                OlderThan1Week = 8,
                Total = 68
            };

            _mockMonitoringService
                .Setup(s => s.GetContractUpdateSummaryAsync(It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var actionResult = await _controller.GetContractUpdateSummary(new List<int> { 1 }, null, null);

            // Assert
            actionResult.Result.Should().BeOfType<OkObjectResult>();
            var okResult = actionResult.Result as OkObjectResult;
            okResult!.Value.Should().BeOfType<ApiResponse<ContractUpdateSummaryResponse>>();
            var apiResponse = okResult.Value as ApiResponse<ContractUpdateSummaryResponse>;
            apiResponse!.Success.Should().BeTrue();
            apiResponse.Data.Should().BeEquivalentTo(expectedSummary);
        }

        [Fact]
        public async Task GetContractsByUpdate_ShouldReturnPagedContracts()
        {
            // Arrange
            var expectedPagedResponse = new PagedContractResponse
            {
                Items = new List<ContractResponse>
                {
                    new ContractResponse
                    {
                        Id = 1,
                        ContractNumber = "CTR-123",
                        TotalAmount = 5000,
                        UpdatedAt = DateTime.UtcNow
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 100
            };

            _mockMonitoringService
                .Setup(s => s.GetContractsByUpdateAsync("lessThan1Hour", It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>(), 1, 100))
                .ReturnsAsync(expectedPagedResponse);

            // Act
            var actionResult = await _controller.GetContractsByUpdate("lessThan1Hour", null, null, null, 1, 100);

            // Assert
            actionResult.Result.Should().BeOfType<OkObjectResult>();
            var okResult = actionResult.Result as OkObjectResult;
            okResult!.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>();
            var apiResponse = okResult.Value as ApiResponse<PagedContractResponse>;
            apiResponse!.Success.Should().BeTrue();
            apiResponse.Data.TotalCount.Should().Be(1);
            apiResponse.Data.Items.Should().HaveCount(1);
            apiResponse.Data.Items[0].ContractNumber.Should().Be("CTR-123");
        }
    }
}
