using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class WizardHeaderValidationTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public WizardHeaderValidationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task Step1Upload_MissingContratoHeader_ShouldReturnErrorMessage()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Contrato" missing
            var csvContent = "Código PV,PV,Matrícula,Comissionado,Grupo,Cota,Versão,Data da Venda,Valor,Nome do Cliente,Tipo,Status\n" +
                             "PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";
            
            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_missing_contrato.csv");

            // Assert
            response.IsTemplateMatch.Should().BeFalse();
            response.MatchMessage.Should().Contain("Contrato");
            response.MatchMessage.Should().Contain("Colunas ausentes: Contrato");
        }

        [Fact]
        public async Task Step1Upload_WithComissionadaAlias_ShouldPassValidation()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with all headers, using "Comissionada" (alias) instead of "Comissionado"
            // Note: REP is no longer mandatory
            var headers = new[] { "Contrato", "Código PV", "PV", "Matrícula", "Comissionada", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_comissionada.csv");

            // Assert
            response.IsTemplateMatch.Should().BeTrue();
            response.MatchMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task Step1Upload_WithMisspelledHeader_ShouldReturnErrorMessage()
        {
             // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Comissionado" misspelled as "Comisionado"
            var headers = new[] { "Contrato", "Código PV", "PV", "Matrícula", "Comisionado", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_misspelled.csv");

            // Assert
            response.IsTemplateMatch.Should().BeFalse();
            response.MatchMessage.Should().Contain("Comissionado");
        }

        [Fact]
        public async Task Step1Upload_WithConsultorAlias_ShouldPassAndExtractUsers()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Consultor" instead of "Comissionado"
            // Note: Also included "Estado" variation for Status as user manually updated the validator
            var headers = new[] { "Contrato", "REP", "Código PV", "PV", "Matrícula", "Consultor", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Estado" };
            var csvContent = string.Join(",", headers) + "\n123,REP-001,PV-101,PV-Center,MAT-999,John Consultant,GR-A,QT-1,V1,2024-04-16,500.00,Alice Customer,Normal,Active";

            // Act: Step 1 Upload
            var preview = await UploadCsvAndGetPreview(csvContent, "test_consultor.csv");

            // Assert: Step 1 Success
            preview.IsTemplateMatch.Should().BeTrue();
            preview.UploadId.Should().NotBeNullOrEmpty();

            // Act: Download Template (Step 2 Preview)
            var templateBytes = await DownloadWizardTemplate(preview.UploadId);
            
            // Assert: Template content contains extracted data
            using var ms = new MemoryStream(templateBytes);
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new OfficeOpenXml.ExcelPackage(ms);
            var sheet = package.Workbook.Worksheets[0];

            var header1 = sheet.Cells[1, 1].Text;
            var header2 = sheet.Cells[1, 2].Text;
            var header4 = sheet.Cells[1, 4].Text;
            
            header1.Should().Be("Name");
            header2.Should().Be("Email");
            header4.Should().Be("Matricula");

            var cellName = sheet.Cells[2, 1].Text;
            var cellMatricula = sheet.Cells[2, 4].Text;

            cellName.Should().Be("John Consultant");
            cellMatricula.Should().Be("MAT-999");
        }

        [Fact]
        public async Task Step1Upload_WithNomePVAlias_ShouldPassValidation()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Nome PV" (alias) instead of "PV"
            var headers = new[] { "Contrato", "Código PV", "Nome PV", "Matrícula", "Comissionado", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_nome_pv.csv");

            // Assert
            response.IsTemplateMatch.Should().BeTrue();
        }

        [Fact]
        public async Task Step1Upload_WithNoVersion_ShouldPassValidation()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Versão" missing (now optional)
            var headers = new[] { "Contrato", "Código PV", "PV", "Matrícula", "Comissionado", "Grupo", "Cota", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,PV1,PV Name,M1,Seller,G1,C1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_no_version.csv");

            // Assert
            response.IsTemplateMatch.Should().BeTrue();
        }

        [Fact]
        public async Task Step1Upload_WithBadHeaderConsult_ShouldReturnFail()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "Consult" (wrong header) instead of "Consultor"
            var headers = new[] { "Contrato", "Código PV", "PV", "Matrícula", "Consult", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_bad_consult.csv");

            // Assert
            response.IsTemplateMatch.Should().BeFalse();
            response.MatchMessage.Should().Contain("Comissionado");
            response.MatchMessage.Should().Contain("Atenção: O arquivo não possui todos os cabeçalhos esperados.");
        }

        private async Task<byte[]> DownloadWizardTemplate(string uploadId)
        {
            var response = await _client.GetAsync($"/api/wizard/step1-template/{uploadId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<ImportPreviewResponse> UploadCsvAndGetPreview(string csvContent, string fileName)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StringContent(csvContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", fileName);

            var response = await _client.PostAsync("/api/wizard/step1-upload", content);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                 throw new Exception($"Unauthorized: {response.StatusCode}. Token might be expired or invalid.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Upload failed: {response.StatusCode} - {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }
    }
}
