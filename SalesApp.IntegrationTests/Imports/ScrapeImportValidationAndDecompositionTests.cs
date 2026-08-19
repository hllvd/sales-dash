using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Imports Tests")]
    public class ScrapeImportValidationAndDecompositionTests
    {
        private readonly ImportsTestFactory _factory;

        public ScrapeImportValidationAndDecompositionTests(ImportsTestFactory factory)
        {
            _factory = factory;
        }

        private async Task<(ImportSession session, Group group, Matricula matricula)> SetupScrapeTestAsync(
            AppDbContext context, 
            IGroupRepository groupRepo,
            IMatriculaRepository matriculaRepo,
            string uploadId)
        {
            var admin = await context.Users.FirstOrDefaultAsync(u => u.Role.Name == "superadmin");
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "pbi_scrape_test.csv",
                Status = "preview",
                UploadedByUserInternalId = admin?.InternalId ?? 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);

            var group = await context.Groups.FirstOrDefaultAsync(g => g.Name == "012171");
            if (group == null)
            {
                group = new Group
                {
                    Name = "012171",
                    IsActive = true
                };
                await groupRepo.CreateAsync(group);
            }

            var matricula = await context.Matriculas.FirstOrDefaultAsync(m => m.MatriculaNumber == "010357");
            if (matricula == null)
            {
                matricula = new Matricula
                {
                    MatriculaNumber = "010357",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await matriculaRepo.CreateAsync(matricula);
            }

            await context.SaveChangesAsync();
            return (session, group, matricula);
        }

        private static Dictionary<string, string> BuildScrapeMappings() => new()
        {
            { "Cota", "ContractNumber" },
            { "Situação Cobrança", "Status" },
            { "Crédito Venda", "TotalAmount" },
            { "Produção Analitica", "TotalAmount" },
            { "Dt Venda", "SaleStartDate" },
            { "Matricula", "MatriculaNumber" }
        };

        [Fact]
        public async Task ScrapeImport_WithComposedCotaString_ShouldDecomposeAndPopulateQuotaAndCustomer()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var contractNumber = "1100708611";
            var composedCota = $"012171;1276;0;JULIO FERNANDO BALANDIUK;{contractNumber}";

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", composedCota },
                    { "Situação Cobrança", "Normal" },
                    { "Produção Analitica", "" }, // Empty -> fallback to Crédito Venda
                    { "Crédito Venda", "100000" },
                    { "Dt Venda", "2026-08-05" },
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            var mappings = BuildScrapeMappings();

            // Act
            var result = await service.ExecuteContractImportAsync(
                uploadId: uploadId, 
                importSessionId: session.Id, 
                rows: rows, 
                mappings: mappings, 
                dateFormat: "yyyy-MM-dd",
                allowAutoCreateGroups: true,
                allowAutoCreatePVs: true);

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);

            var createdContract = await context.Contracts
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);

            createdContract.Should().NotBeNull();
            createdContract!.ContractNumber.Should().Be("1100708611");
            createdContract.CustomerName.Should().Be("JULIO FERNANDO BALANDIUK");
            createdContract.Quota.Should().Be(1276, because: "Quota (Cota number) must be decomposed from position 2 in the Cota string");
            createdContract.TotalAmount.Should().Be(100000);
            createdContract.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task ScrapeImport_Rule1_InvalidDateFormat_ShouldSilentlySkipNewContract()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "012171;1276;0;JULIO FERNANDO BALANDIUK;1100708612" },
                    { "Situação Cobrança", "Normal" },
                    { "Crédito Venda", "100000" },
                    { "Dt Venda", "INVALID-DATE-FORMAT" }, // Rule 1: Invalid date
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            var mappings = BuildScrapeMappings();

            // Act
            var result = await service.ExecuteContractImportAsync(
                uploadId: uploadId, 
                importSessionId: session.Id, 
                rows: rows, 
                mappings: mappings, 
                dateFormat: "yyyy-MM-dd",
                allowAutoCreateGroups: true);

            // Assert: Silently skipped, no contract created, 0 failed rows
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(0);

            var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == "1100708612");
            contract.Should().BeNull();
        }

        [Fact]
        public async Task ScrapeImport_Rule3_MissingTotalAmountAndCreditoVenda_ShouldSilentlySkipNewContract()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "012171;1276;0;JULIO FERNANDO BALANDIUK;1100708613" },
                    { "Situação Cobrança", "Normal" },
                    { "Produção Analitica", "0" },
                    { "Crédito Venda", "0" }, // Rule 3: Both Produção Analitica and Crédito Venda are 0
                    { "Dt Venda", "2026-08-05" },
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            var mappings = BuildScrapeMappings();

            // Act
            var result = await service.ExecuteContractImportAsync(
                uploadId: uploadId, 
                importSessionId: session.Id, 
                rows: rows, 
                mappings: mappings, 
                dateFormat: "yyyy-MM-dd",
                allowAutoCreateGroups: true);

            // Assert: Silently skipped
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(0);

            var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == "1100708613");
            contract.Should().BeNull();
        }

        [Fact]
        public async Task ScrapeImport_Rule4_MissingSituacaoCobranca_ShouldSilentlySkipNewContract()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", "012171;1276;0;JULIO FERNANDO BALANDIUK;1100708614" },
                    { "Situação Cobrança", "" }, // Rule 4: Blank status
                    { "Crédito Venda", "100000" },
                    { "Dt Venda", "2026-08-05" },
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            var mappings = BuildScrapeMappings();

            // Act
            var result = await service.ExecuteContractImportAsync(
                uploadId: uploadId, 
                importSessionId: session.Id, 
                rows: rows, 
                mappings: mappings, 
                dateFormat: "yyyy-MM-dd",
                allowAutoCreateGroups: true);

            // Assert: Silently skipped
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(0);

            var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == "1100708614");
            contract.Should().BeNull();
        }

        [Fact]
        public async Task ScrapeImport_ExistingContract_ShouldUpdateQuotaAndRetainContract()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var statusService = scope.ServiceProvider.GetRequiredService<IContractStatusService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();
            var contractRepo = scope.ServiceProvider.GetRequiredService<IContractRepository>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var contractNumber = "1100708615";
            var activeStatusId = await statusService.GetStatusIdByNameAsync("Active");

            // Pre-create existing contract with missing Quota
            var existingContract = new Contract
            {
                ContractNumber = contractNumber,
                TotalAmount = 50000,
                GroupId = group.Id,
                MatriculaId = matricula.Id,
                ContractStatusId = activeStatusId,
                SaleStartDate = new DateTime(2026, 1, 1),
                Quota = null, // Currently null
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await contractRepo.CreateAsync(existingContract);

            var composedCota = $"012171;9999;0;EXISTING CUSTOMER;{contractNumber}";

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", composedCota },
                    { "Situação Cobrança", "Normal" },
                    { "Crédito Venda", "100000" },
                    { "Dt Venda", "2026-08-05" },
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            var mappings = BuildScrapeMappings();

            // Act
            var result = await service.ExecuteContractImportAsync(
                uploadId: uploadId, 
                importSessionId: session.Id, 
                rows: rows, 
                mappings: mappings, 
                dateFormat: "yyyy-MM-dd",
                allowAutoCreateGroups: true);

            // Assert
            result.ProcessedRows.Should().Be(1);

            var updatedContract = await context.Contracts
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);

            updatedContract.Should().NotBeNull();
            updatedContract!.Quota.Should().Be(9999, because: "Existing contract's Quota must be updated from decomposed Cota string");
            updatedContract.TotalAmount.Should().Be(100000);
        }
    }
}
