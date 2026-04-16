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
        public async Task Step1Upload_MissingRepHeader_ShouldReturnErrorMessage()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with "REP" missing
            var csvContent = "Contrato,Código PV,PV,Matrícula,Comissionado,Grupo,Cota,Versão,Data da Venda,Valor,Nome do Cliente,Tipo,Status\n" +
                             "123,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";
            
            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_missing_rep.csv");

            // Assert
            response.IsTemplateMatch.Should().BeFalse();
            response.MatchMessage.Should().Contain("REP");
            response.MatchMessage.Should().Contain("Atenção: O arquivo não possui todos os cabeçalhos esperados.");
        }

        [Fact]
        public async Task Step1Upload_WithComissionadaAlias_ShouldPassValidation()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a CSV with all headers, using "Comissionada" (alias) instead of "Comissionado"
            var headers = new[] { "Contrato", "REP", "Código PV", "PV", "Matrícula", "Comissionada", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,R1,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

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
            var headers = new[] { "Contrato", "REP", "Código PV", "PV", "Matrícula", "Comisionado", "Grupo", "Cota", "Versão", "Data da Venda", "Valor", "Nome do Cliente", "Tipo", "Status" };
            var csvContent = string.Join(",", headers) + "\n123,R1,PV1,PV Name,M1,Seller,G1,C1,V1,2024-01-01,100,Customer,T1,Active";

            // Act
            var response = await UploadCsvAndGetPreview(csvContent, "test_misspelled.csv");

            // Assert
            response.IsTemplateMatch.Should().BeFalse();
            response.MatchMessage.Should().Contain("Comissionado");
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
