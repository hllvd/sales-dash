using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OfficeOpenXml;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ExportServiceTests
    {
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly Mock<IServiceScope> _mockScope;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly ExportService _service;

        public ExportServiceTests()
        {
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            _mockScope = new Mock<IServiceScope>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockContractRepository = new Mock<IContractRepository>();

            _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
            _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IContractRepository)))
                .Returns(_mockContractRepository.Object);

            _service = new ExportService(_mockScopeFactory.Object);
        }

        [Fact]
        public async Task StartExport_WithMoreThan2000Rows_ShouldCreateMultipleSheets()
        {
            // Arrange
            var contracts = new List<Contract>();
            for (int i = 0; i < 2500; i++)
            {
                contracts.Add(new Contract
                {
                    Id = i + 1,
                    ContractNumber = $"C{i + 1}",
                    TotalAmount = 100 * (i + 1),
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _mockContractRepository.Setup(x => x.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>()))
                .ReturnsAsync(contracts);

            var filters = new ContractExportRequest();
            var scope = new UserScopeContext { IsGlobal = true };
            var userId = Guid.NewGuid().ToString();

            // Act
            var jobId = _service.StartExport(filters, scope, userId);

            // Wait for background task to complete (or poll status)
            ExportJobResponse? status = null;
            int attempts = 0;
            do
            {
                await Task.Delay(100);
                status = _service.GetJobStatus(jobId);
                attempts++;
            } while (status != null && status.Status != "completed" && attempts < 50);

            // Assert
            status.Should().NotBeNull();
            status!.Status.Should().Be("completed");
            status.ProcessedRows.Should().Be(2500);

            var bytes = _service.GetJobBytes(jobId, userId);
            bytes.Should().NotBeNull();

            using var stream = new MemoryStream(bytes!);
            using var package = new ExcelPackage(stream);

            // Verify multiple sheets (2000 + 500)
            package.Workbook.Worksheets.Count.Should().Be(2);
            package.Workbook.Worksheets[0].Name.Should().Be("Contratos 1");
            package.Workbook.Worksheets[1].Name.Should().Be("Contratos 2");

            // Check row counts (including header)
            package.Workbook.Worksheets[0].Dimension.End.Row.Should().Be(2001);
            package.Workbook.Worksheets[1].Dimension.End.Row.Should().Be(501);
        }

        [Fact]
        public async Task StartExport_WithNoRows_ShouldStillCreateOneSheetWithHeaders()
        {
            // Arrange
            _mockContractRepository.Setup(x => x.GetAllAsync(
                It.IsAny<Guid?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UserScopeContext?>(),
                It.IsAny<List<int>?>(),
                It.IsAny<List<Guid>?>()))
                .ReturnsAsync(new List<Contract>());

            var filters = new ContractExportRequest();
            var scope = new UserScopeContext { IsGlobal = true };
            var userId = Guid.NewGuid().ToString();

            // Act
            var jobId = _service.StartExport(filters, scope, userId);

            // Wait for completion
            ExportJobResponse? status = null;
            int attempts = 0;
            do
            {
                await Task.Delay(100);
                status = _service.GetJobStatus(jobId);
                attempts++;
            } while (status != null && status.Status != "completed" && attempts < 50);

            // Assert
            status.Should().NotBeNull();
            status!.Status.Should().Be("completed");
            var bytes = _service.GetJobBytes(jobId, userId);
            bytes.Should().NotBeNull();
            
            using var stream = new MemoryStream(bytes!);
            using var package = new ExcelPackage(stream);

            package.Workbook.Worksheets.Count.Should().Be(1);
            package.Workbook.Worksheets[0].Dimension.End.Row.Should().Be(1); // Only header
            package.Workbook.Worksheets[0].Cells[1, 1].Value.Should().Be("Nº Contrato");
        }
    }
}
