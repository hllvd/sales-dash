using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Imports Tests")]
    public class ConsultorImportWorkflowTests
    {
        private readonly HttpClient _client;
        private readonly ImportsTestFactory _factory;

        public ConsultorImportWorkflowTests(ImportsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task UploadConsultorFile_ViaDashboardUpload_ShouldAutoDetectConsultorDashboardTemplate()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // CSV with consultor readable columns
            var csv = @"Nº Contrato,Grupo,Cota,Versão,Consorciado,Cod.Comissionado,R$ Bem Venda,Data.Venda,Definição.Situação,Quant.Parcelas Pagas,Quant.Parcelas em Atraso,Data.Cancelamento.Cota,Data Primeira Assembleia
CTR-CONS-001,1001,10,1,Cliente Um,MAT-CONS-1,50000.00,2026-01-10,Normal,3,0,,2026-02-15
CTR-CONS-002,1001,20,1,Cliente Dois,MAT-CONS-1,75000.00,2026-01-15,Excluido,0,0,2026-01-20,2026-02-15";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", "consultor_export.csv");

            // Act - Uploading to dashboard endpoint (which defaults to template 3)
            var response = await _client.PostAsync("/api/imports/dashboard/upload", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Transparently detected as consultorDashboard!
            result.Data!.TemplateName.Should().Be("consultorDashboard");
            result.Data.TemplateId.Should().Be(4);
            result.Data.SuggestedMappings.Should().ContainKey("Nº Contrato");
            result.Data.SuggestedMappings["Nº Contrato"].Should().Be("ContractNumber");
            result.Data.SuggestedMappings.Should().ContainKey("R$ Bem Venda");
            result.Data.SuggestedMappings["R$ Bem Venda"].Should().Be("TotalAmount");
            result.Data.SuggestedMappings.Should().ContainKey("Cod.Comissionado");
            result.Data.SuggestedMappings["Cod.Comissionado"].Should().Be("MatriculaNumber");
        }

        [Fact]
        public async Task CompleteConsultorImport_ShouldMapHasPaymentAndDesistenteCorrectly()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var csv = @"Nº Contrato,Grupo,Cota,Versão,Consorciado,Cod.Comissionado,R$ Bem Venda,Data.Venda,Definição.Situação,Quant.Parcelas Pagas,Quant.Parcelas em Atraso,Data.Cancelamento.Cota,Data Primeira Assembleia
CTR-PAY-1,2001,101,1,Cliente Pago,MAT-PAY,100000.00,2026-01-10,Normal,2,0,,2026-02-15
CTR-DESIST-1,2001,102,1,Cliente Desistente,MAT-PAY,80000.00,2026-01-10,Excluido,0,0,2026-01-20,2026-02-15
CTR-DEFAULT-1,2001,103,1,Cliente Cancelado Com Pagto,MAT-PAY,90000.00,2026-01-10,Excluido,1,0,2026-03-20,2026-02-15
,2001,104,1,Cliente Sem Contrato,MAT-PAY,60000.00,2026-01-10,Normal,1,0,,2026-02-15";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", "consultor_full.csv");

            // 1. Upload
            var uploadResponse = await _client.PostAsync("/api/imports/dashboard/upload", content);
            uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            var uploadId = uploadResult!.Data!.UploadId;

            // 2. Configure Mappings
            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = uploadResult.Data.SuggestedMappings,
                AllowAutoCreateGroups = true,
                AllowAutoCreatePVs = true,
                SkipMissingContractNumber = true
            };

            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/dashboard/{uploadId}/mappings", mappingRequest);
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Confirm Import
            var confirmRequest = new ConfirmImportRequest
            {
                SkipMissingContractNumber = true,
                AllowAutoCreateGroups = true,
                AllowAutoCreatePVs = true
            };

            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/dashboard/{uploadId}/confirm", confirmRequest);
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify Database
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Contract 1: Paid -> HasPayment = true, Status = Active
            var c1 = await db.Contracts.Include(c => c.ContractStatus).FirstOrDefaultAsync(c => c.ContractNumber == "CTR-PAY-1");
            c1.Should().NotBeNull();
            c1!.HasPayment.Should().BeTrue();
            c1.ContractStatus.Name.Should().Be("Active");
            c1.TotalAmount.Should().Be(100000.00m);

            // Contract 2: Excluido + 0 paid + cancellation before assembly -> Desistente
            var c2 = await db.Contracts.Include(c => c.ContractStatus).FirstOrDefaultAsync(c => c.ContractNumber == "CTR-DESIST-1");
            c2.Should().NotBeNull();
            c2!.HasPayment.Should().BeFalse();
            c2.ContractStatus.Name.Should().Be("Desistente");

            // Contract 3: Excluido + 1 paid -> Defaulted (not Desistente)
            var c3 = await db.Contracts.Include(c => c.ContractStatus).FirstOrDefaultAsync(c => c.ContractNumber == "CTR-DEFAULT-1");
            c3.Should().NotBeNull();
            c3!.HasPayment.Should().BeTrue();
            c3.ContractStatus.Name.Should().Be("Defaulted");

            // Empty contract number row should be skipped
            var emptyContracts = await db.Contracts.Where(c => c.UploadId == uploadId && string.IsNullOrEmpty(c.ContractNumber)).ToListAsync();
            emptyContracts.Should().BeEmpty();
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
