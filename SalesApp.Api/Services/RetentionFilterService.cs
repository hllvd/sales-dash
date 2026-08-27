using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SalesApp.DTOs;
using SalesApp.Libs;
using SalesApp.Utils;

namespace SalesApp.Services
{
    public class RetentionFilterService : IRetentionFilterService
    {
        static RetentionFilterService()
        {
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
        }

        public async Task<RetentionFilterProcessResponse> ProcessFilterPreviewAsync(
            Stream streamA, string fileNameA, Stream streamB, string fileNameB)
        {
            ValidateStreams(streamA, fileNameA, streamB, fileNameB);

            var contractsB = await ExtractContractNumbersFromStreamBAsync(streamB, fileNameB);
            var (headers, rowsA) = await ReadModelARowsAsync(streamA, fileNameA);

            var (matchedRows, stats, matchedContracts) = FilterRows(headers, rowsA, contractsB);

            var sampleRows = matchedRows
                .Take(50)
                .Select(row =>
                {
                    var dict = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count && i < row.Count; i++)
                    {
                        dict[headers[i]] = row[i];
                    }
                    return dict;
                })
                .ToList();

            return new RetentionFilterProcessResponse
            {
                Stats = stats,
                MatchedContracts = matchedContracts.ToList(),
                Headers = headers,
                SampleRows = sampleRows
            };
        }

        public async Task<RetentionFilterExportResult> FilterAndGenerateWorkbookAsync(
            Stream streamA, string fileNameA, Stream streamB, string fileNameB)
        {
            ValidateStreams(streamA, fileNameA, streamB, fileNameB);

            var contractsB = await ExtractContractNumbersFromStreamBAsync(streamB, fileNameB);
            var (headers, rowsA) = await ReadModelARowsAsync(streamA, fileNameA);

            var (matchedRows, stats, _) = FilterRows(headers, rowsA, contractsB);

            var fileBytes = GenerateExcelPackage(headers, matchedRows);

            return new RetentionFilterExportResult
            {
                FileBytes = fileBytes,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = "modelo_retencao_filtrado.xlsx",
                Stats = stats
            };
        }

        #region Pure Core Logic

        private static (List<List<string>> MatchedRows, RetentionFilterStatsDto Stats, List<string> MatchedContracts) FilterRows(
            List<string> headers,
            List<List<string>> rowsA,
            HashSet<string> contractsB)
        {
            var matchedRows = new List<List<string>>();
            var matchedContracts = new List<string>();
            int removedCount = 0;

            // Find contract column index fallback if any
            int contractColIndex = -1;
            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                if (h.Contains("contrat") || h.Contains("contract"))
                {
                    contractColIndex = i;
                    break;
                }
            }

            foreach (var row in rowsA)
            {
                if (row == null || row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
                    continue;

                var resolvedContract = ExtractContractFromRow(row, contractColIndex);

                if (!string.IsNullOrWhiteSpace(resolvedContract) && contractsB.Contains(resolvedContract))
                {
                    matchedRows.Add(row);
                    matchedContracts.Add(resolvedContract);
                }
                else
                {
                    removedCount++;
                }
            }

            int totalRowsA = matchedRows.Count + removedCount;
            double retentionRate = totalRowsA > 0 ? Math.Round((double)matchedRows.Count / totalRowsA * 100.0, 2) : 0.0;

            var stats = new RetentionFilterStatsDto
            {
                TotalRowsModelA = totalRowsA,
                TotalContractsModelB = contractsB.Count,
                MatchedRowsModelC = matchedRows.Count,
                RemovedRows = removedCount,
                RetentionRate = retentionRate
            };

            return (matchedRows, stats, matchedContracts);
        }

        public static string? ExtractContractFromRow(List<string> row, int fallbackColIndex)
        {
            if (row == null || row.Count == 0) return null;

            // 1. First column decomposition (canonical modelo_retencao composed format)
            var firstCell = row[0];
            if (!string.IsNullOrWhiteSpace(firstCell))
            {
                var decomposed = CotaDecomposer.Decompose(firstCell).Contract;
                if (!string.IsNullOrWhiteSpace(decomposed))
                {
                    return NormalizationUtils.NormalizeNumber(decomposed);
                }
            }

            // 2. Fallback to identified contract column index
            if (fallbackColIndex >= 0 && fallbackColIndex < row.Count)
            {
                var val = row[fallbackColIndex];
                if (!string.IsNullOrWhiteSpace(val))
                {
                    var decomposed = CotaDecomposer.Decompose(val).Contract;
                    if (!string.IsNullOrWhiteSpace(decomposed))
                    {
                        return NormalizationUtils.NormalizeNumber(decomposed);
                    }
                }
            }

            return null;
        }

        #endregion

        #region Boundary Parsers and Generators

        private static void ValidateStreams(Stream streamA, string fileNameA, Stream streamB, string fileNameB)
        {
            if (streamA == null || streamA.Length == 0)
                throw new ArgumentException("O arquivo Modelo A está vazio ou inválido.");

            if (streamB == null || streamB.Length == 0)
                throw new ArgumentException("O arquivo Modelo B está vazio ou inválido.");

            var extA = Path.GetExtension(fileNameA).ToLowerInvariant();
            var extB = Path.GetExtension(fileNameB).ToLowerInvariant();

            if (extA != ".xlsx" && extA != ".csv")
                throw new ArgumentException("Formato inválido para o Modelo A. Por favor envie .xlsx ou .csv.");

            if (extB != ".xlsx" && extB != ".csv")
                throw new ArgumentException("Formato inválido para o Modelo B. Por favor envie .xlsx ou .csv.");
        }

        private static async Task<HashSet<string>> ExtractContractNumbersFromStreamBAsync(Stream stream, string fileName)
        {
            var contracts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var package = new ExcelPackage(memoryStream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null || worksheet.Dimension == null)
                    return contracts;

                int rowCount = worksheet.Dimension.Rows;
                for (int row = 1; row <= rowCount; row++)
                {
                    var cellVal = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(cellVal)) continue;

                    // Skip header row if it contains text words like "contrato", "contract"
                    if (row == 1 && (cellVal.Contains("contrat", StringComparison.OrdinalIgnoreCase) ||
                                     cellVal.Contains("número", StringComparison.OrdinalIgnoreCase) ||
                                     cellVal.Contains("numero", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var normalized = NormalizationUtils.NormalizeNumber(cellVal);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        contracts.Add(normalized);
                    }
                }
            }
            else // .csv
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
                string? line;
                int lineIndex = 0;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineIndex++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var delimiter = line.Contains(';') ? ';' : ',';
                    var parts = line.Split(delimiter);
                    var cellVal = parts[0].Trim().Trim('"');

                    if (lineIndex == 1 && (cellVal.Contains("contrat", StringComparison.OrdinalIgnoreCase) ||
                                           cellVal.Contains("número", StringComparison.OrdinalIgnoreCase) ||
                                           cellVal.Contains("numero", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var normalized = NormalizationUtils.NormalizeNumber(cellVal);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        contracts.Add(normalized);
                    }
                }
            }

            return contracts;
        }

        private static async Task<(List<string> Headers, List<List<string>> Rows)> ReadModelARowsAsync(Stream stream, string fileName)
        {
            var headers = new List<string>();
            var rows = new List<List<string>>();
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var package = new ExcelPackage(memoryStream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null || worksheet.Dimension == null)
                    return (headers, rows);

                int colCount = worksheet.Dimension.Columns;
                int rowCount = worksheet.Dimension.Rows;

                // Find effective last column header
                int effectiveCols = 0;
                for (int col = colCount; col >= 1; col--)
                {
                    if (!string.IsNullOrWhiteSpace(worksheet.Cells[1, col].Value?.ToString()))
                    {
                        effectiveCols = col;
                        break;
                    }
                }
                if (effectiveCols == 0) effectiveCols = colCount;

                // Read Headers
                for (int col = 1; col <= effectiveCols; col++)
                {
                    var h = worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? $"Coluna_{col}";
                    headers.Add(h);
                }

                // Read Data Rows
                for (int row = 2; row <= rowCount; row++)
                {
                    var rowData = new List<string>();
                    bool hasAnyVal = false;
                    for (int col = 1; col <= effectiveCols; col++)
                    {
                        var val = worksheet.Cells[row, col].Value?.ToString()?.Trim() ?? string.Empty;
                        rowData.Add(val);
                        if (!string.IsNullOrEmpty(val)) hasAnyVal = true;
                    }
                    if (hasAnyVal)
                    {
                        rows.Add(rowData);
                    }
                }
            }
            else // .csv
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
                string? line;
                bool isFirst = true;
                char delimiter = ';';

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (isFirst)
                    {
                        delimiter = line.Contains(';') ? ';' : ',';
                        headers = line.Split(delimiter).Select(h => h.Trim().Trim('"')).ToList();
                        isFirst = false;
                        continue;
                    }

                    var cells = line.Split(delimiter).Select(c => c.Trim().Trim('"')).ToList();
                    rows.Add(cells);
                }
            }

            return (headers, rows);
        }

        private static byte[] GenerateExcelPackage(List<string> headers, List<List<string>> matchedRows)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Retenção Filtrada");

            // Header row
            for (int c = 0; c < headers.Count; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
                ws.Cells[1, c + 1].Style.Font.Bold = true;
                ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(243, 244, 246));
                ws.Cells[1, c + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                ws.Cells[1, c + 1].Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(209, 213, 219));
            }

            // Data rows
            for (int r = 0; r < matchedRows.Count; r++)
            {
                var rowData = matchedRows[r];
                for (int c = 0; c < headers.Count; c++)
                {
                    var val = c < rowData.Count ? rowData[c] : string.Empty;

                    // Numeric detection for clean formatting
                    if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
                    {
                        ws.Cells[r + 2, c + 1].Value = numVal;
                    }
                    else
                    {
                        ws.Cells[r + 2, c + 1].Value = val;
                    }
                }
            }

            // Auto-fit columns
            ws.Cells[1, 1, Math.Max(1, matchedRows.Count + 1), Math.Max(1, headers.Count)].AutoFitColumns();

            return package.GetAsByteArray();
        }

        #endregion
    }
}
