using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    /// <summary>
    /// Singleton export service. Manages in-memory export jobs.
    /// Jobs and their XLSX bytes expire 10 minutes after creation.
    /// </summary>
    public class ExportService : IExportService
    {
        private const int ExpiryMinutes = 10;
        private const int RowsPerSheet = 2000;

        private readonly IServiceScopeFactory _scopeFactory;

        private sealed record ExportJob(
            string JobId,
            string RequestingUserId,
            DateTime CreatedAt,
            ContractExportRequest? Filters = null,
            UserScopeContext? Scope = null,
            string? FilterId = null
        )
        {
            public string Status { get; set; } = "pending";
            public int TotalRows { get; set; }
            public int ProcessedRows { get; set; }
            public string? ErrorMessage { get; set; }
            public byte[]? FileBytes { get; set; }
        }

        private readonly ConcurrentDictionary<string, ExportJob> _jobs = new();

        public ExportService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
        }

        public string StartExport(ContractExportRequest filters, UserScopeContext scope, string requestingUserId)
        {
            PurgeExpiredJobs();

            var jobId = Guid.NewGuid().ToString("N");
            var job = new ExportJob(jobId, requestingUserId, DateTime.UtcNow, Filters: filters, Scope: scope);
            _jobs[jobId] = job;

            // Fire-and-forget background task
            _ = Task.Run(() => ProcessExportAsync(jobId));

            return jobId;
        }

        public string StartReportExport(string filterId, string requestingUserId)
        {
            PurgeExpiredJobs();

            var jobId = Guid.NewGuid().ToString("N");
            var job = new ExportJob(jobId, requestingUserId, DateTime.UtcNow, FilterId: filterId);
            _jobs[jobId] = job;

            // Fire-and-forget background task
            _ = Task.Run(() => ProcessReportExportAsync(jobId));

            return jobId;
        }

        public ExportJobResponse? GetJobStatus(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || IsExpired(job))
            {
                _jobs.TryRemove(jobId, out _);
                return null;
            }

            return MapToResponse(job);
        }

        public byte[]? GetJobBytes(string jobId, string requestingUserId)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || IsExpired(job))
            {
                _jobs.TryRemove(jobId, out _);
                return null;
            }

            // Security: only the requesting user can download their own job
            if (job.RequestingUserId != requestingUserId)
                return null;

            return job.Status == "completed" ? job.FileBytes : null;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task ProcessReportExportAsync(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job)) return;

            job.Status = "processing";

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reportService = scope.ServiceProvider.GetRequiredService<SalesApp.ReportFilters.Services.IReportFilterService>();

                // Get all results (page size 1,000,000 to get everything)
                // callerId is the same as requestingUserId
                var currentUserId = Guid.TryParse(job.RequestingUserId, out var guid) ? (Guid?)guid : null;
                var result = await reportService.ExecuteAsync(job.RequestingUserId, job.FilterId!, currentUserId, 1, 1000000);

                if (!result.Success)
                    throw new Exception("Failed to execute report");

                var data = result.Data!;
                job.TotalRows = data.TotalCount;

                using var package = new ExcelPackage();
                var sheetIndex = 1;
                var sheetRows = 0;
                ExcelWorksheet? ws = null;

                // Headers from the report projection
                var headers = data.Columns.OrderBy(c => c.Order).Select(c => c.Label).ToArray();

                for (var i = 0; i < data.Rows.Count; i++)
                {
                    if (ws == null || sheetRows >= RowsPerSheet)
                    {
                        ws = package.Workbook.Worksheets.Add($"Relatório {sheetIndex++}");
                        AddHeaders(ws, headers);
                        sheetRows = 0;
                    }

                    var row = sheetRows + 2;
                    var rowData = data.Rows[i];

                    for (var col = 0; col < headers.Length; col++)
                    {
                        var val = rowData.GetValueOrDefault(headers[col]);
                        ws.Cells[row, col + 1].Value = val;
                        
                        // Numeric formatting hint
                        if (val is decimal || val is double || val is float)
                        {
                            ws.Cells[row, col + 1].Style.Numberformat.Format = "#,##0.00";
                        }
                    }

                    sheetRows++;
                    job.ProcessedRows = i + 1;
                }

                // Empty state
                if (data.Rows.Count == 0)
                {
                    ws = package.Workbook.Worksheets.Add("Relatório 1");
                    AddHeaders(ws, headers);
                }

                foreach (var sheet in package.Workbook.Worksheets)
                {
                    sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
                }

                job.FileBytes = package.GetAsByteArray();
                job.Status = "completed";
            }
            catch (Exception ex)
            {
                if (_jobs.TryGetValue(jobId, out var failedJob))
                {
                    failedJob.Status = "failed";
                    failedJob.ErrorMessage = ex.Message;
                }
            }
        }

        private async Task ProcessExportAsync(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Filters == null) return;

            job.Status = "processing";

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var contractRepo = scope.ServiceProvider.GetRequiredService<IContractRepository>();

                var contracts = await contractRepo.GetAllAsync(
                    job.Filters.UserId,
                    job.Filters.GroupId,
                    job.Filters.StartDate,
                    job.Filters.EndDate,
                    job.Filters.ContractNumber,
                    job.Filters.ShowUnassigned,
                    job.Filters.Matricula,
                    job.Filters.UserEmail,
                    job.Scope,
                    job.Filters.TeamIds,
                    job.Filters.UserIds);

                job.TotalRows = contracts.Count;

                // Build XLSX with EPPlus — paginate into sheets of 2000 rows
                using var package = new ExcelPackage();

                var sheetIndex = 1;
                var sheetRows = 0;
                ExcelWorksheet? ws = null;

                // Column headers definition
                var headers = new[]
                {
                    "Nº Contrato", "Usuário", "Email", "Matrícula",
                    "Grupo", "Cliente", "Valor Total", "Status",
                    "Tipo", "Cota", "Data Início", "Criado Em"
                };

                for (var i = 0; i < contracts.Count; i++)
                {
                    if (ws == null || sheetRows >= RowsPerSheet)
                    {
                        ws = package.Workbook.Worksheets.Add($"Contratos {sheetIndex++}");
                        AddHeaders(ws, headers);
                        sheetRows = 0;
                    }

                    var row = sheetRows + 2; // +2 because row 1 is header
                    var c = contracts[i];

                    // Resolve matricula
                    var matriculaNumber = c.Matricula?.MatriculaNumber ?? c.TempMatricula ?? string.Empty;

                    ws.Cells[row, 1].Value = c.ContractNumber;
                    ws.Cells[row, 2].Value = c.User?.Name ?? string.Empty;
                    ws.Cells[row, 3].Value = c.User?.Email ?? string.Empty;
                    ws.Cells[row, 4].Value = matriculaNumber;
                    ws.Cells[row, 5].Value = c.Group?.Name ?? string.Empty;
                    ws.Cells[row, 6].Value = c.CustomerName ?? string.Empty;
                    ws.Cells[row, 7].Value = c.TotalAmount;
                    ws.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[row, 8].Value = c.ContractStatus?.Name ?? string.Empty;
                    ws.Cells[row, 9].Value = c.ContractType.HasValue
                        ? ContractTypeExtensions.ToApiString(c.ContractType)
                        : string.Empty;
                    ws.Cells[row, 10].Value = c.Quota;
                    ws.Cells[row, 11].Value = c.SaleStartDate.ToString("dd/MM/yyyy");
                    ws.Cells[row, 12].Value = c.CreatedAt.ToString("dd/MM/yyyy");

                    sheetRows++;
                    job.ProcessedRows = i + 1;
                }

                // If no contracts, still create one empty sheet with headers
                if (contracts.Count == 0)
                {
                    ws = package.Workbook.Worksheets.Add("Contratos 1");
                    AddHeaders(ws, headers);
                }

                // Auto-fit columns in each sheet
                foreach (var sheet in package.Workbook.Worksheets)
                {
                    sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
                }

                job.FileBytes = package.GetAsByteArray();
                job.Status = "completed";
            }
            catch (Exception ex)
            {
                if (_jobs.TryGetValue(jobId, out var failedJob))
                {
                    failedJob.Status = "failed";
                    failedJob.ErrorMessage = ex.Message;
                }
            }
        }

        private static void AddHeaders(ExcelWorksheet ws, string[] headers)
        {
            for (var col = 0; col < headers.Length; col++)
            {
                var cell = ws.Cells[1, col + 1];
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 41, 55)); // dark header
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }
        }

        private static bool IsExpired(ExportJob job) =>
            DateTime.UtcNow - job.CreatedAt > TimeSpan.FromMinutes(ExpiryMinutes);

        private static ExportJobResponse MapToResponse(ExportJob job) => new()
        {
            JobId = job.JobId,
            Status = job.Status,
            TotalRows = job.TotalRows,
            ProcessedRows = job.ProcessedRows,
            ErrorMessage = job.ErrorMessage
        };

        private void PurgeExpiredJobs()
        {
            foreach (var kvp in _jobs)
            {
                if (IsExpired(kvp.Value))
                    _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
