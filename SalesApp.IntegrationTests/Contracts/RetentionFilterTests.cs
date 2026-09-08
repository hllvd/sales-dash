using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using OfficeOpenXml;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Contracts Tests")]
    public class RetentionFilterTests
    {
        private readonly ContractsTestFactory _factory;
        private readonly HttpClient _client;

        public RetentionFilterTests(ContractsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
            ExcelPackage.License.SetNonCommercialOrganization("SalesAppTest");
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "superadmin@test.com",
                password = "superadmin123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        private static byte[] CreateSampleXlsx(List<string> headers, List<List<string>> rows)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Sheet1");
            for (int c = 0; c < headers.Count; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
            }
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < headers.Count; c++)
                {
                    ws.Cells[r + 2, c + 1].Value = c < rows[r].Count ? rows[r][c] : "";
                }
            }
            return package.GetAsByteArray();
        }

        [Fact]
        public async Task RetentionFilter_PreviewAndDownloadEndpoints_ShouldWorkEndToEnd()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var headersA = new List<string> { "Cota", "Valor", "Cliente" };
            var rowsA = new List<List<string>>
            {
                new() { "012173;4103;0;CLIENTE 1;10001", "1000", "CLIENTE 1" },
                new() { "012173;4103;0;CLIENTE 2;10002", "2000", "CLIENTE 2" },
                new() { "012173;4103;0;CLIENTE 3;10003", "3000", "CLIENTE 3" }
            };
            var fileABytes = CreateSampleXlsx(headersA, rowsA);

            var headersB = new List<string> { "Contrato" };
            var rowsB = new List<List<string>>
            {
                new() { "10001" },
                new() { "10003" }
            };
            var fileBBytes = CreateSampleXlsx(headersB, rowsB);

            // 1. Test Preview Endpoint
            using (var formData = new MultipartFormDataContent())
            {
                var contentA = new ByteArrayContent(fileABytes);
                contentA.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                formData.Add(contentA, "fileA", "model_a.xlsx");

                var contentB = new ByteArrayContent(fileBBytes);
                contentB.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                formData.Add(contentB, "fileB", "model_b.xlsx");

                var previewResponse = await _client.PostAsync("/api/retentionfilter/preview", formData);
                previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

                var previewResult = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<RetentionFilterProcessResponse>>();
                previewResult!.Success.Should().BeTrue();
                previewResult.Data.Should().NotBeNull();
                previewResult.Data!.Stats.TotalRowsModelA.Should().Be(3);
                previewResult.Data.Stats.MatchedRowsModelC.Should().Be(2);
                previewResult.Data.Stats.RemovedRows.Should().Be(1);
                previewResult.Data.MatchedContracts.Should().Contain(new[] { "10001", "10003" });
            }

            // 2. Test Download Endpoint
            using (var formData = new MultipartFormDataContent())
            {
                var contentA = new ByteArrayContent(fileABytes);
                contentA.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                formData.Add(contentA, "fileA", "model_a.xlsx");

                var contentB = new ByteArrayContent(fileBBytes);
                contentB.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                formData.Add(contentB, "fileB", "model_b.xlsx");

                var downloadResponse = await _client.PostAsync("/api/retentionfilter/download", formData);
                downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                downloadResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
                downloadedBytes.Should().NotBeEmpty();

                using var downloadedPkg = new ExcelPackage(new MemoryStream(downloadedBytes));
                var ws = downloadedPkg.Workbook.Worksheets[0];
                ws.Dimension.Rows.Should().Be(3); // Header + 2 matches
            }
        }
    }
}
