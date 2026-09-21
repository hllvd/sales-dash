using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Models.Configuration;
using SalesApp.Repositories;
using Amazon.S3;
using Amazon.S3.Model;

namespace SalesApp.Services
{
    public interface IScrapeImportService
    {
        Task<ImportResult> AutoImportAsync(string filePath, Guid? userId);
        Task<ImportResult> AutoImportFromS3Async(string bucket, string s3Key, Guid? userId);
    }

    public class ScrapeImportService : IScrapeImportService
    {
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly IImportExecutionService _importService;
        private readonly IImportSessionRepository _sessionRepository;
        private readonly ScrapeImportOptions _options;
        private readonly IAmazonS3 _s3;

        public ScrapeImportService(
            AppDbContext context,
            IUserRepository userRepository,
            IImportExecutionService importService,
            IImportSessionRepository sessionRepository,
            IOptions<ScrapeImportOptions> options,
            IAmazonS3 s3)
        {
            _context = context;
            _userRepository = userRepository;
            _importService = importService;
            _sessionRepository = sessionRepository;
            _options = options.Value;
            _s3 = s3;
        }

        public async Task<ImportResult> AutoImportAsync(string filePath, Guid? userId)
        {
            var result = new ImportResult();
            
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            User? user = null;
            if (userId.HasValue)
            {
                user = await _userRepository.GetByIdAsync(userId.Value);
                if (user == null)
                {
                    result.Errors.Add($"User {userId} not found");
                    return result;
                }
            }

            return await RunImportFromFileAsync(filePath, Path.GetFileName(filePath), user);
        }

        public async Task<ImportResult> AutoImportFromS3Async(string bucket, string s3Key, Guid? userId)
        {
            var result = new ImportResult();

            User? user = null;
            if (userId.HasValue)
            {
                user = await _userRepository.GetByIdAsync(userId.Value);
                if (user == null)
                {
                    result.Errors.Add($"User {userId} not found");
                    return result;
                }
            }

            // Download CSV from S3 to a temp file
            var tmpPath = Path.Combine(Path.GetTempPath(), $"s3import_{Guid.NewGuid()}.csv");
            try
            {
                var getRequest = new GetObjectRequest { BucketName = bucket, Key = s3Key };
                using var response = await _s3.GetObjectAsync(getRequest);
                await response.WriteResponseStreamToFileAsync(tmpPath, false, CancellationToken.None);

                return await RunImportFromFileAsync(tmpPath, Path.GetFileName(s3Key), user);
            }
            catch (AmazonS3Exception s3Ex)
            {
                result.Errors.Add($"Amazon S3 Error ({s3Ex.ErrorCode}): {s3Ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error downloading or importing from S3 ({s3Key}): {ex.Message}");
                return result;
            }
            finally
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
        }

        private async Task<ImportResult> RunImportFromFileAsync(string filePath, string fileName, User? user)
        {
            var result = new ImportResult();

            // Create an import session for tracking
            var session = new ImportSession
            {
                UploadId = $"scrape-{DateTime.UtcNow:yyyyMMddHHmmss}",
                FileName = fileName,
                FileType = "csv",
                UploadedByUserInternalId = user?.InternalId ?? 0,
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
                        
                        rows.Add(rowDict);
                    }
                }

                if (!rows.Any())
                {
                    session.Status = "Completed";
                    await _sessionRepository.UpdateAsync(session);
                    return result;
                }

                ScrapeConfig? scrapeConfig = null;
                if (user != null)
                {
                    scrapeConfig = await _context.ScrapeConfigs
                        .Where(c => c.UserInternalId == user.InternalId)
                        .OrderByDescending(c => c.UpdatedAt)
                        .FirstOrDefaultAsync();
                }

                bool skipMissingContractNumber = scrapeConfig?.SkipMissingContractNumber ?? true;
                bool allowAutoCreateGroups = scrapeConfig?.AllowAutoCreateGroups ?? true;
                bool allowAutoCreatePVs = scrapeConfig?.AllowAutoCreatePVs ?? true;
                bool updateMatriculaOnExisting = scrapeConfig?.UpdateMatriculaOnExisting ?? false;
                bool updateTotalAmountOnExisting = scrapeConfig?.UpdateTotalAmountOnExisting ?? true;
                bool updateStartDateOnExisting = scrapeConfig?.UpdateStartDateOnExisting ?? true;

                // Handoff to the dashboard contract import execution service
                var importResult = await _importService.ExecuteContractDashboardImportAsync(
                    uploadId: $"pbi-scrape-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    importSessionId: session.Id,
                    rows: rows,
                    mappings: _options.Mappings,
                    skipMissingContractNumber: skipMissingContractNumber,
                    allowAutoCreateGroups: allowAutoCreateGroups,
                    allowAutoCreatePVs: allowAutoCreatePVs,
                    updateMatriculaOnExisting: updateMatriculaOnExisting,
                    updateTotalAmountOnExisting: updateTotalAmountOnExisting,
                    updateStartDateOnExisting: updateStartDateOnExisting
                );

                if (importResult.ProcessedRows == 0 && rows.Count > 0)
                {
                    var keys = string.Join(", ", rows.First().Keys);
                    result.Errors.Add($"All {rows.Count} rows were skipped! Possible mapping mismatch. Available columns in CSV: {keys}");
                }

                if (importResult.Errors.Any())
                {
                    result.Errors.AddRange(importResult.Errors);
                }

                session.Status = importResult.Errors.Any() ? "Failed" : "Completed";
                session.TotalRows = importResult.TotalRows;
                session.ProcessedRows = importResult.ProcessedRows;
                session.FailedRows = importResult.FailedRows;
                session.CompletedAt = DateTime.UtcNow;
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
