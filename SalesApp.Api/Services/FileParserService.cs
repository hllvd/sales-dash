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
                return await ParseCsvAsync(file);
            }
            else if (fileType == "xlsx")
            {
                return await ParseExcelAsync(file);
            }
            
            throw new ArgumentException($"Unsupported file type: {fileType}");
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

        private async Task<List<Dictionary<string, string>>> ParseCsvAsync(IFormFile file)
        {
            var rows = new List<Dictionary<string, string>>();
            
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null // Ignore missing fields
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

        private async Task<List<Dictionary<string, string>>> ParseExcelAsync(IFormFile file)
        {
            var rows = new List<Dictionary<string, string>>();
            
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                throw new ArgumentException("Excel file contains no worksheets");
            }

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            var columnCount = worksheet.Dimension?.Columns ?? 0;

            // Read headers from first row
            var headers = new List<string>();
            for (int col = 1; col <= columnCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? string.Empty;
                headers.Add(headerValue);
            }

            // Read data rows (starting from row 2)
            for (int row = 2; row <= rowCount; row++)
            {
                var rowData = new Dictionary<string, string>();
                for (int col = 1; col <= columnCount; col++)
                {
                    var header = headers[col - 1];
                    var cellValue = worksheet.Cells[row, col].Value?.ToString()?.Trim() ?? string.Empty;
                    rowData[header] = cellValue;
                }
                rows.Add(rowData);
            }

            return await Task.FromResult(rows);
        }
    }
}
