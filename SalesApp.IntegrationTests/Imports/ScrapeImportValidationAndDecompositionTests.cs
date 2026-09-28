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

        [Fact]
        public async Task ScrapeImport_Geral_ShouldMapTemPagamento_And_PreserveDesistente()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();
            var statusService = scope.ServiceProvider.GetRequiredService<IContractStatusService>();
            var scrapeOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SalesApp.Models.Configuration.ScrapeImportOptions>>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var cotaDesistente = "1100999001";
            var cotaNormal = "1100999002";

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Cota", $"012171;1001;0;CUSTOMER DESISTENTE;{cotaDesistente}" },
                    { "tbl_cotas.status_cota", "Desistente" },
                    { "Crédito Venda", "150000" },
                    { "Dt Produção", "2026-08-01" },
                    { "tbl_cotas.tem_pagamento", "Não" },
                    { "Matricula", matricula.MatriculaNumber }
                },
                new()
                {
                    { "Cota", $"012171;1002;0;CUSTOMER NORMAL;{cotaNormal}" },
                    { "tbl_cotas.status_cota", "Normal" },
                    { "Crédito Venda", "200000" },
                    { "Dt Produção", "2026-08-02" },
                    { "tbl_cotas.tem_pagamento", "Sim" },
                    { "Matricula", matricula.MatriculaNumber }
                }
            };

            // Act
            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId: uploadId,
                importSessionId: session.Id,
                rows: rows,
                mappings: scrapeOptions.Value.Mappings,
                skipMissingContractNumber: true,
                allowAutoCreateGroups: true,
                allowAutoCreatePVs: true);

            // Assert
            result.ProcessedRows.Should().Be(2);

            var desistenteStatusId = await statusService.GetStatusIdByNameAsync("Desistente");
            var activeStatusId = await statusService.GetStatusIdByNameAsync("Active");

            var contract1 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaDesistente);
            contract1.Should().NotBeNull();
            contract1!.ContractStatusId.Should().Be(desistenteStatusId, "Status Desistente must be preserved and mapped to Desistente");
            contract1.HasPayment.Should().BeFalse("tbl_cotas.tem_pagamento = 'Não' must yield HasPayment = false");

            var contract2 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaNormal);
            contract2.Should().NotBeNull();
            contract2!.ContractStatusId.Should().Be(activeStatusId);
            contract2.HasPayment.Should().BeTrue("tbl_cotas.tem_pagamento = 'Sim' must yield HasPayment = true");
        }

        [Fact]
        public async Task ScrapeImport_Consultor_ShouldDeduceHasPaymentFromUltPagtoParcela()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();
            var statusService = scope.ServiceProvider.GetRequiredService<IContractStatusService>();
            var scrapeOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SalesApp.Models.Configuration.ScrapeImportOptions>>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var cotaWithPayment = "1100999003";
            var cotaWithoutPayment = "1100999004";

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaWithPayment },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "120000" },
                    { "2 Rel Carteira.Data.Venda", "2026-07-15" },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2001" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE COM PAGAMENTO" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "2026-09-14" }
                },
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaWithoutPayment },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "80000" },
                    { "2 Rel Carteira.Data.Venda", "2026-07-20" },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2002" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE SEM PAGAMENTO" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "" } // Null/empty last payment date
                }
            };

            // Act
            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId: uploadId,
                importSessionId: session.Id,
                rows: rows,
                mappings: scrapeOptions.Value.Mappings,
                skipMissingContractNumber: true,
                allowAutoCreateGroups: true,
                allowAutoCreatePVs: true);

            // Assert
            result.ProcessedRows.Should().Be(2);

            var contract1 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaWithPayment);
            contract1.Should().NotBeNull();
            contract1!.HasPayment.Should().BeTrue("2 Rel Carteira.Últ.Pagto Parcela with date must yield HasPayment = true");

            var contract2 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaWithoutPayment);
            contract2.Should().NotBeNull();
            contract2!.HasPayment.Should().BeFalse("2 Rel Carteira.Últ.Pagto Parcela empty must yield HasPayment = false");
        }

        [Fact]
        public async Task ScrapeImport_Consultor_ShouldDeriveLateAndDesistenteStatuses()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var matriculaRepo = scope.ServiceProvider.GetRequiredService<IMatriculaRepository>();
            var statusService = scope.ServiceProvider.GetRequiredService<IContractStatusService>();
            var scrapeOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SalesApp.Models.Configuration.ScrapeImportOptions>>();

            var uploadId = $"pbi-scrape-{Guid.NewGuid():N}";
            var (session, group, matricula) = await SetupScrapeTestAsync(context, groupRepo, matriculaRepo, uploadId);

            var cotaLate1 = "1100999011";
            var cotaLate2 = "1100999012";
            var cotaLate3 = "1100999013";
            var cotaDesistente = "1100999014";
            var cotaDefaulted = "1100999015";
            var cotaAwaiting = "1100999016";

            var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var pastDateStr = DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd");

            var rows = new List<Dictionary<string, string>>
            {
                // Late1
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaLate1 },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", pastDateStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2011" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE LATE 1" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "2026-08-10" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas em Atraso)", "1" }
                },
                // Late2
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaLate2 },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", pastDateStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2012" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE LATE 2" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "2026-07-10" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas em Atraso)", "2" }
                },
                // Late3 (4 atrasos)
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaLate3 },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", pastDateStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2013" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE LATE 3" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "2026-05-10" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas em Atraso)", "4" }
                },
                // Desistente: Excluido + 0 parcelas pagas
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaDesistente },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", pastDateStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2014" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE DESISTENTE" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Excluido" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas Pagas)", "0" }
                },
                // Defaulted: Excluido + 3 parcelas pagas
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaDefaulted },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", pastDateStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2015" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE DEFAULTED" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Excluido" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas Pagas)", "3" }
                },
                // AwaitingPayment: Normal + no payment ("0") + recent sale (<= 30 days)
                new()
                {
                    { "Sum(2 Rel Carteira.n_contrato)", cotaAwaiting },
                    { "Sum(2 Rel Carteira.R$ Bem Venda)", "100000" },
                    { "2 Rel Carteira.Data.Venda", todayStr },
                    { "2 Rel Carteira.Grupo", "012171" },
                    { "Sum(2 Rel Carteira.Cota)", "2016" },
                    { "2 Rel Carteira.Consorciado", "CLIENTE RECENTE SEM PAGTO" },
                    { "2 Rel Carteira.Cod.Comissionado", matricula.MatriculaNumber },
                    { "2 Rel Carteira.Definição.Situação", "Normal" },
                    { "2 Rel Carteira.Últ.Pagto Parcela", "0" },
                    { "Sum(2 Rel Carteira.Quant.Parcelas em Atraso)", "0" }
                }
            };

            // Act
            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId: uploadId,
                importSessionId: session.Id,
                rows: rows,
                mappings: scrapeOptions.Value.Mappings,
                skipMissingContractNumber: true,
                allowAutoCreateGroups: true,
                allowAutoCreatePVs: true);

            // Assert
            result.ProcessedRows.Should().Be(6);

            var late1Id = await statusService.GetStatusIdByNameAsync("Late1");
            var late2Id = await statusService.GetStatusIdByNameAsync("Late2");
            var late3Id = await statusService.GetStatusIdByNameAsync("Late3");
            var desistenteId = await statusService.GetStatusIdByNameAsync("Desistente");
            var defaultedId = await statusService.GetStatusIdByNameAsync("Defaulted");
            var awaitingId = await statusService.GetStatusIdByNameAsync("AwaitingPayment");

            var c1 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaLate1);
            c1.Should().NotBeNull();
            c1!.ContractStatusId.Should().Be(late1Id, "Normal + 1 atraso must yield Late1");

            var c2 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaLate2);
            c2.Should().NotBeNull();
            c2!.ContractStatusId.Should().Be(late2Id, "Normal + 2 atrasos must yield Late2");

            var c3 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaLate3);
            c3.Should().NotBeNull();
            c3!.ContractStatusId.Should().Be(late3Id, "Normal + 4 atrasos must yield Late3");

            var c4 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaDesistente);
            c4.Should().NotBeNull();
            c4!.ContractStatusId.Should().Be(desistenteId, "Excluido + 0 parcelas pagas must yield Desistente");

            var c5 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaDefaulted);
            c5.Should().NotBeNull();
            c5!.ContractStatusId.Should().Be(defaultedId, "Excluido + 3 parcelas pagas must yield Defaulted");

            var c6 = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == cotaAwaiting);
            c6.Should().NotBeNull();
            c6!.ContractStatusId.Should().Be(awaitingId, "Normal + Ult.Pagto = 0 + Venda <= 30 dias must yield AwaitingPayment");
            c6.HasPayment.Should().BeFalse("Ult.Pagto = 0 must yield HasPayment = false");
        }
    }
}
