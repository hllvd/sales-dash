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
                MoreThan1Hour = 5,
                MoreThan3Hours = 10,
                MoreThan6Hours = 15,
                MoreThan1Day = 25,
                MoreThan3Days = 40,
                MoreThan1Week = 8,
                Total = 68
            };

            _mockMonitoringService
                .Setup(s => s.GetContractUpdateSummaryAsync(It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var actionResult = await _controller.GetContractUpdateSummary(new List<int> { 1 }, null, null, null, null);

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
                .Setup(s => s.GetContractsByUpdateAsync("moreThan1Hour", It.IsAny<List<int>?>(), It.IsAny<List<Guid>?>(), It.IsAny<List<string>?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 100))
                .ReturnsAsync(expectedPagedResponse);

            // Act
            var actionResult = await _controller.GetContractsByUpdate("moreThan1Hour", null, null, null, null, null, 1, 100);

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
