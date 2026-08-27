using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using OfficeOpenXml;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class RetentionFilterServiceTests
    {
        private readonly RetentionFilterService _service;

        public RetentionFilterServiceTests()
        {
            ExcelPackage.License.SetNonCommercialOrganization("SalesAppTest");
            _service = new RetentionFilterService();
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
        public async Task ProcessFilterPreview_WithComposedColumns_ShouldMatchContractsCorrectly()
        {
            // Arrange
            var headersA = new List<string> { "Cota", "Valor", "Cliente", "Status" };
            var rowsA = new List<List<string>>
            {
                new() { "012173;4103;0;MARIO;1100326334", "150000", "MARIO SILVA", "Ativa" },
                new() { "012173;4103;0;JOAO;1100326335", "200000", "JOAO SOUZA", "Ativa" },
                new() { "012173;4103;0;ANA;1100326336", "300000", "ANA PAULA", "Cancelada" }
            };
            var fileABytes = CreateSampleXlsx(headersA, rowsA);

            var headersB = new List<string> { "Contrato" };
            var rowsB = new List<List<string>>
            {
                new() { "1100326334" },
                new() { "1100326336" }
            };
            var fileBBytes = CreateSampleXlsx(headersB, rowsB);

            using var streamA = new MemoryStream(fileABytes);
            using var streamB = new MemoryStream(fileBBytes);

            // Act
            var result = await _service.ProcessFilterPreviewAsync(streamA, "modelA.xlsx", streamB, "modelB.xlsx");

            // Assert
            result.Should().NotBeNull();
            result.Stats.TotalRowsModelA.Should().Be(3);
            result.Stats.TotalContractsModelB.Should().Be(2);
            result.Stats.MatchedRowsModelC.Should().Be(2);
            result.Stats.RemovedRows.Should().Be(1);
            result.Stats.RetentionRate.Should().BeApproximately(66.67, 0.1);
            result.MatchedContracts.Should().Contain(new[] { "1100326334", "1100326336" });
            result.SampleRows.Should().HaveCount(2);
            result.SampleRows[0]["Cliente"].Should().Be("MARIO SILVA");
            result.SampleRows[1]["Cliente"].Should().Be("ANA PAULA");
        }

        [Fact]
        public async Task FilterAndGenerateWorkbook_WithLeadingZeros_ShouldNormalizeAndMatch()
        {
            // Arrange
            var headersA = new List<string> { "Contrato", "Valor", "Cliente" };
            var rowsA = new List<List<string>>
            {
                new() { "000971937", "100000", "Sergio Santos" },
                new() { "863497", "120000", "Kleyton Santos" },
                new() { "837754", "105590", "Richard Wesley" }
            };
            var fileABytes = CreateSampleXlsx(headersA, rowsA);

            // Model B has normalized contract (no leading zeros for 971937) and one with leading zeros
            var headersB = new List<string> { "Numero Contrato" };
            var rowsB = new List<List<string>>
            {
                new() { "971937" },
                new() { "000837754" }
            };
            var fileBBytes = CreateSampleXlsx(headersB, rowsB);

            using var streamA = new MemoryStream(fileABytes);
            using var streamB = new MemoryStream(fileBBytes);

            // Act
            var exportResult = await _service.FilterAndGenerateWorkbookAsync(streamA, "modelA.xlsx", streamB, "modelB.xlsx");

            // Assert
            exportResult.Should().NotBeNull();
            exportResult.Stats.MatchedRowsModelC.Should().Be(2);
            exportResult.Stats.RemovedRows.Should().Be(1);
            exportResult.FileBytes.Should().NotBeEmpty();

            // Verify generated XLSX
            using var generatedPackage = new ExcelPackage(new MemoryStream(exportResult.FileBytes));
            var ws = generatedPackage.Workbook.Worksheets.FirstOrDefault();
            ws.Should().NotBeNull();
            ws!.Dimension.Rows.Should().Be(3); // 1 header + 2 matched rows
            ws.Cells[1, 1].Value.Should().Be("Contrato");
            ws.Cells[1, 2].Value.Should().Be("Valor");
            ws.Cells[1, 3].Value.Should().Be("Cliente");
        }

        [Fact]
        public async Task ProcessFilterPreview_WithCsvModelB_ShouldParseAndFilterCorrectly()
        {
            // Arrange
            var headersA = new List<string> { "Contrato", "Valor" };
            var rowsA = new List<List<string>>
            {
                new() { "1001", "500" },
                new() { "1002", "1500" },
                new() { "1003", "2500" }
            };
            var fileABytes = CreateSampleXlsx(headersA, rowsA);

            var csvB = "Contratos\n1002\n1003\n";
            var fileBBytes = Encoding.UTF8.GetBytes(csvB);

            using var streamA = new MemoryStream(fileABytes);
            using var streamB = new MemoryStream(fileBBytes);

            // Act
            var result = await _service.ProcessFilterPreviewAsync(streamA, "modelA.xlsx", streamB, "modelB.csv");

            // Assert
            result.Stats.MatchedRowsModelC.Should().Be(2);
            result.Stats.TotalContractsModelB.Should().Be(2);
            result.MatchedContracts.Should().Contain(new[] { "1002", "1003" });
        }

        [Fact]
        public async Task ProcessFilterPreview_WithEmptyStream_ShouldThrowArgumentException()
        {
            using var emptyStreamA = new MemoryStream();
            using var streamB = new MemoryStream(new byte[] { 1, 2, 3 });

            Func<Task> act = async () => await _service.ProcessFilterPreviewAsync(emptyStreamA, "a.xlsx", streamB, "b.xlsx");

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Modelo A está vazio*");
        }

        [Fact]
        public async Task FilterAndGenerateWorkbook_WithNoMatches_ShouldReturnHeaderOnlyWorkbook()
        {
            // Arrange
            var headersA = new List<string> { "Contrato", "Valor" };
            var rowsA = new List<List<string>>
            {
                new() { "1001", "500" },
                new() { "1002", "1500" }
            };
            var fileABytes = CreateSampleXlsx(headersA, rowsA);

            var headersB = new List<string> { "Contrato" };
            var rowsB = new List<List<string>>
            {
                new() { "9999" }
            };
            var fileBBytes = CreateSampleXlsx(headersB, rowsB);

            using var streamA = new MemoryStream(fileABytes);
            using var streamB = new MemoryStream(fileBBytes);

            // Act
            var exportResult = await _service.FilterAndGenerateWorkbookAsync(streamA, "modelA.xlsx", streamB, "modelB.xlsx");

            // Assert
            exportResult.Stats.MatchedRowsModelC.Should().Be(0);
            exportResult.Stats.RemovedRows.Should().Be(2);
            exportResult.Stats.RetentionRate.Should().Be(0.0);

            using var generatedPackage = new ExcelPackage(new MemoryStream(exportResult.FileBytes));
            var ws = generatedPackage.Workbook.Worksheets.FirstOrDefault();
            ws.Should().NotBeNull();
            ws!.Dimension.Rows.Should().Be(1); // Only header
        }
    }
}
