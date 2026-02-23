using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml;
using System.Globalization;

namespace SalesApp.Services
{
    public class FileParserService : IFileParserService
    {
        public FileParserService()
        {
            // Set EPPlus license context (required for EPPlus 5+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public string GetFileType(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return extension switch
            {
                ".csv" => "csv",
                ".xlsx" => "xlsx",
                _ => throw new ArgumentException($"Unsupported file type: {extension}")
            };
        }

        public async Task<List<string>> GetColumnsAsync(IFormFile file)
        {
            var fileType = GetFileType(file);
            
            if (fileType == "csv")
            {
                return await GetCsvColumnsAsync(file);
            }
            else if (fileType == "xlsx")
            {
                return await GetExcelColumnsAsync(file);
            }
            
            throw new ArgumentException($"Unsupported file type: {fileType}");
        }

        public async Task<List<Dictionary<string, string>>> ParseFileAsync(IFormFile file)
        {
            var fileType = GetFileType(file);

            if (fileType == "csv")
            {
                return await ParseCsvDirectAsync(file);
            }
            else if (fileType == "xlsx")
            {
                return await ParseExcelDirectAsync(file);
            }

            throw new ArgumentException($"Unsupported file type: {fileType}");
        }

        // ✅ Reads the ENTIRE CSV stream into a string buffer first, then parses from StringReader.
        // This guarantees that CsvHelper reads from a finite in-memory string (not an HTTP body stream
        // that may never signal EOF in some edge cases).
        private async Task<List<Dictionary<string, string>>> ParseCsvDirectAsync(IFormFile file)
        {
            string csvContent;
            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                csvContent = await reader.ReadToEndAsync();
            }

            var rows = new List<Dictionary<string, string>>();
            using var stringReader = new StringReader(csvContent);
            using var csv = new CsvReader(stringReader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null
            });

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (await csv.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header)?.Trim() ?? string.Empty;
                }

                // Skip fully-empty rows
                if (row.Values.All(v => string.IsNullOrWhiteSpace(v)))
                    continue;

                rows.Add(row);
            }

            return rows;
        }

        // ✅ Copies the XLSX stream into a MemoryStream buffer first, then parses with EPPlus.
        // Stops at 5 consecutive empty rows (correct EOF detection — Dimension.Rows can be 1M+).
        private async Task<List<Dictionary<string, string>>> ParseExcelDirectAsync(IFormFile file)
        {
            byte[] fileBytes;
            using (var stream = file.OpenReadStream())
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var rows = new List<Dictionary<string, string>>();
            using var package = new ExcelPackage(new MemoryStream(fileBytes));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return rows;

            var columnCount = worksheet.Dimension?.Columns ?? 0;
            if (columnCount == 0) return rows;

            var headers = new List<string>();
            for (int col = 1; col <= columnCount; col++)
            {
                headers.Add(worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? string.Empty);
            }

            const int maxConsecutiveEmptyRows = 5;
            int consecutiveEmpty = 0;
            int rowNum = 2;

            while (true)
            {
                var rowData = new Dictionary<string, string>();
                bool isEmpty = true;

                for (int col = 1; col <= columnCount; col++)
                {
                    var cell = worksheet.Cells[rowNum, col];
                    string val = cell.Value is DateTime dt
                        ? dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        : cell.Value?.ToString()?.Trim() ?? string.Empty;

                    rowData[headers[col - 1]] = val;
                    if (!string.IsNullOrWhiteSpace(val)) isEmpty = false;
                }

                if (isEmpty)
                {
                    if (++consecutiveEmpty >= maxConsecutiveEmptyRows) break;
                    rowNum++;
                    continue;
                }

                consecutiveEmpty = 0;
                rows.Add(rowData);
                rowNum++;
            }

            return rows;
        }


        public async IAsyncEnumerable<Dictionary<string, string>> ParseFileStreamedAsync(IFormFile file)
        {
            var fileType = GetFileType(file);
            
            if (fileType == "csv")
            {
                await foreach (var row in ParseCsvStreamedAsync(file))
                {
                    yield return row;
                }
            }
            else if (fileType == "xlsx")
            {
                await foreach (var row in ParseExcelStreamedAsync(file))
                {
                    yield return row;
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported file type: {fileType}");
            }
        }

        private async Task<List<string>> GetCsvColumnsAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim
            });

            await csv.ReadAsync();
            csv.ReadHeader();
            return csv.HeaderRecord?.ToList() ?? new List<string>();
        }

        private async IAsyncEnumerable<Dictionary<string, string>> ParseCsvStreamedAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null
            });

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (await csv.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header)?.Trim() ?? string.Empty;
                }

                // Skip fully-empty rows (e.g. trailing blank lines in CSV)
                if (row.Values.All(v => string.IsNullOrWhiteSpace(v)))
                    continue;

                yield return row;
            }
        }

        private async Task<List<Dictionary<string, string>>> ParseCsvAsync(IFormFile file)
        {
            var rows = new List<Dictionary<string, string>>();
            await foreach (var row in ParseCsvStreamedAsync(file))
            {
                rows.Add(row);
            }
            return rows;
        }

        private async Task<List<string>> GetExcelColumnsAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                throw new ArgumentException("Excel file contains no worksheets");
            }

            var columns = new List<string>();
            var columnCount = worksheet.Dimension?.Columns ?? 0;
            
            for (int col = 1; col <= columnCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(headerValue))
                {
                    columns.Add(headerValue);
                }
            }

            return await Task.FromResult(columns);
        }

        private async IAsyncEnumerable<Dictionary<string, string>> ParseExcelStreamedAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                throw new ArgumentException("Excel file contains no worksheets");
            }

            var columnCount = worksheet.Dimension?.Columns ?? 0;
            // ✅ FIX: Do NOT use worksheet.Dimension.Rows as the row limit.
            // EPPlus sets Dimension.Rows to the last "used" row per Excel's internal tracking,
            // which can be 1,048,576 even when only a handful of rows have real data
            // (e.g. a cell was clicked / formatted far below the data and saved).
            // Instead we iterate until we find consecutive empty rows, which is the
            // correct end-of-data signal for spreadsheet files.
            const int maxConsecutiveEmptyRows = 5;
            int consecutiveEmptyRows = 0;

            // Read headers from row 1
            var headers = new List<string>();
            for (int col = 1; col <= columnCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? string.Empty;
                headers.Add(headerValue);
            }

            // Read data rows starting from row 2, stop on consecutive empties
            int row = 2;
            while (true)
            {
                var rowData = new Dictionary<string, string>();
                bool rowIsEmpty = true;

                for (int col = 1; col <= columnCount; col++)
                {
                    var header = headers[col - 1];
                    var cell = worksheet.Cells[row, col];
                    string cellValue;

                    if (cell.Value is DateTime dt)
                    {
                        cellValue = dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        cellValue = cell.Value?.ToString()?.Trim() ?? string.Empty;
                    }

                    rowData[header] = cellValue;
                    if (!string.IsNullOrWhiteSpace(cellValue))
                        rowIsEmpty = false;
                }

                if (rowIsEmpty)
                {
                    consecutiveEmptyRows++;
                    if (consecutiveEmptyRows >= maxConsecutiveEmptyRows)
                        break; // true end-of-data reached
                    row++;
                    continue;
                }

                consecutiveEmptyRows = 0;
                yield return rowData;
                row++;
            }
        }

        private async Task<List<Dictionary<string, string>>> ParseExcelAsync(IFormFile file)
        {
            var rows = new List<Dictionary<string, string>>();
            await foreach (var row in ParseExcelStreamedAsync(file))
            {
                rows.Add(row);
            }
            return rows;
        }
    }
}
