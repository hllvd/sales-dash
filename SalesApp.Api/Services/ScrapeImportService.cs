using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Models.Configuration;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public interface IScrapeImportService
    {
        Task<ImportResult> AutoImportAsync(string filePath, Guid userId);
    }

    public class ScrapeImportService : IScrapeImportService
    {
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly IImportExecutionService _importService;
        private readonly IImportSessionRepository _sessionRepository;
        private readonly ScrapeImportOptions _options;

        public ScrapeImportService(
            AppDbContext context,
            IUserRepository userRepository,
            IImportExecutionService importService,
            IImportSessionRepository sessionRepository,
            IOptions<ScrapeImportOptions> options)
        {
            _context = context;
            _userRepository = userRepository;
            _importService = importService;
            _sessionRepository = sessionRepository;
            _options = options.Value;
        }

        public async Task<ImportResult> AutoImportAsync(string filePath, Guid userId)
        {
            var result = new ImportResult();
            
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                result.Errors.Add($"User {userId} not found");
                return result;
            }

            // Create an import session for tracking
            var session = new ImportSession
            {
                UploadId = $"scrape-{DateTime.UtcNow:yyyyMMddHHmmss}",
                FileName = Path.GetFileName(filePath),
                FileType = "csv",
                UploadedByUserInternalId = user.InternalId,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };
            await _sessionRepository.CreateAsync(session);

            try
            {
                var rows = new List<Dictionary<string, string>>();
                
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, config))
                {
                    var records = csv.GetRecords<dynamic>();
                    foreach (var record in records)
                    {
                        var row = (IDictionary<string, object>)record;
                        var rowDict = new Dictionary<string, string>();
                        
                        foreach (var kvp in row)
                        {
                            rowDict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                        
                        // UserEmail is no longer injected; we will dynamically resolve ownership via Matricula
                        
                        rows.Add(rowDict);
                    }
                }

                if (!rows.Any())
                {
                    session.Status = "Completed";
                    await _sessionRepository.UpdateAsync(session);
                    return result;
                }

                // Handoff to the standard ImportExecutionService
                var importResult = await _importService.ExecuteContractImportAsync(
                    uploadId: $"pbi-scrape-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    importSessionId: session.Id,
                    rows: rows,
                    mappings: _options.Mappings,
                    dateFormat: "dd/MM/yyyy", // Standard Brazilian format often used in PBI exports
                    skipMissingContractNumber: true,
                    allowAutoCreateGroups: true,
                    allowAutoCreatePVs: true
                );

                // Add robust logging to catch missing mappings or silent skips
                if (importResult.ProcessedRows == 0 && rows.Count > 0)
                {
                    var keys = string.Join(", ", rows.First().Keys);
                    result.Errors.Add($"All {rows.Count} rows were skipped! Possible mapping mismatch. Available columns in CSV: {keys}");
                }

                // Append any inner errors to the main result so they get saved to DynamoDB
                if (importResult.Errors.Any())
                {
                    result.Errors.AddRange(importResult.Errors);
                }

                // Finalize session status
                session.Status = importResult.Errors.Any() ? "Failed" : "Completed";
                await _sessionRepository.UpdateAsync(session);

                return importResult;
            }
            catch (Exception ex)
            {
                session.Status = "Failed";
                await _sessionRepository.UpdateAsync(session);
                result.Errors.Add($"Scrape import failed: {ex.Message}");
                return result;
            }
        }
    }
}
