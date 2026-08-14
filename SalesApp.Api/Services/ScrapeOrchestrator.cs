using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using Microsoft.AspNetCore.DataProtection;

namespace SalesApp.Services
{
    public interface IScrapeOrchestrator
    {
        Task<string> TriggerScrapeAsync(int configId, bool isManual = true, string? runId = null, string? userEmail = null);
        Task HandleCallbackAsync(ScrapeResult result);
    }

    public class ScrapeOrchestrator : IScrapeOrchestrator
    {
        private readonly AppDbContext _context;
        private readonly PbiScraperClient _scraperClient;
        private readonly IScrapeDynamoLogService _logService;
        private readonly IScrapeImportService _importService;
        private readonly IDataProtector _protector;
        private readonly string _outputDir;

        public ScrapeOrchestrator(
            AppDbContext context,
            PbiScraperClient scraperClient,
            IScrapeDynamoLogService logService,
            IScrapeImportService importService,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration)
        {
            _context = context;
            _scraperClient = scraperClient;
            _logService = logService;
            _importService = importService;
            _protector = dataProtectionProvider.CreateProtector("ScrapeConfig.PowerBiPassword");
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
        }

        public async Task<string> TriggerScrapeAsync(int configId, bool isManual = true, string? runId = null, string? userEmail = null)
        {
            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == configId);

            if (config == null) throw new ArgumentException($"Config {configId} not found");

            var jobId = Guid.NewGuid().ToString();
            var effectiveRunId = string.IsNullOrEmpty(runId) ? Guid.NewGuid().ToString() : runId;
            var effectiveUserEmail = userEmail;

            if (string.IsNullOrEmpty(effectiveUserEmail))
            {
                effectiveUserEmail = config.User?.Email ?? string.Empty;
                if (string.IsNullOrEmpty(effectiveUserEmail) && config.UserId.HasValue)
                {
                    var u = await _context.Users.FindAsync(config.UserId.Value);
                    effectiveUserEmail = u?.Email ?? string.Empty;
                }
            }
            
            // 1. Log start in DynamoDB
            await _logService.WriteJobStatusAsync(
                jobId: jobId,
                userId: config.UserId?.ToString() ?? string.Empty,
                status: "Pending",
                store: config.Store,
                matricula: config.Matricula,
                runId: effectiveRunId,
                userEmail: effectiveUserEmail,
                additionalData: new { IsManual = isManual }
            );

            // 2. Call Node.js service
            try
            {
                string? decryptedPassword = config.PowerBiPassword;

                await _scraperClient.EnqueueJobAsync(
                    jobId: jobId,
                    userId: config.UserId?.ToString() ?? string.Empty,
                    store: config.Store,
                    matricula: config.Matricula,
                    avaproUsername: config.Matricula, // Using Matricula as username for PBI login
                    avaproPassword: decryptedPassword,
                    runId: effectiveRunId
                );
                
                // Update status to Running
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId?.ToString() ?? string.Empty,
                    status: "Running",
                    store: config.Store,
                    matricula: config.Matricula,
                    runId: effectiveRunId,
                    userEmail: effectiveUserEmail
                );
            }
            catch (Exception ex)
            {
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId?.ToString() ?? string.Empty,
                    status: "Failed",
                    store: config.Store,
                    matricula: config.Matricula,
                    runId: effectiveRunId,
                    userEmail: effectiveUserEmail,
                    additionalData: new { ErrorMessage = ex.Message }
                );
                throw;
            }

            return jobId;
        }

        public async Task HandleCallbackAsync(ScrapeResult result)
        {
            // 1. Update DynamoDB status
            await _logService.WriteJobStatusAsync(
                jobId: result.JobId,
                userId: result.UserId,
                status: result.Status,
                store: result.Store ?? "Unknown",
                matricula: result.Matricula ?? "Unknown",
                runId: result.RunId,
                additionalData: new
                {
                    RowCount = result.RowCount,
                    FileRelativePath = result.FileRelativePath,
                    ErrorMessage = result.Error,
                    CompletedAt = DateTime.UtcNow.ToString("O"),
                    AuthStatus = result.AuthStatus,
                    AuthMessage = result.AuthMessage,
                    PowerBiLoaded = result.PowerBiLoaded,
                    AuthSteps = result.AuthSteps != null ? Newtonsoft.Json.JsonConvert.SerializeObject(result.AuthSteps) : null
                }
            );

            // 2. Trigger Auto-Import if success
            if (result.Status == "Succeeded" && !string.IsNullOrEmpty(result.FileRelativePath))
            {
                var filePath = Path.Combine(_outputDir, result.FileRelativePath);
                try
                {
                    var importResult = await _importService.AutoImportAsync(filePath, Guid.Parse(result.UserId));
                    
                    if (importResult.Errors.Any() || importResult.Warnings.Any())
                    {
                        var statusStr = importResult.Errors.Any() ? "Failed" : "Succeeded";
                        var errorMsg = importResult.Errors.Any() ? $"Importação falhou: {string.Join(" | ", importResult.Errors)}" : null;
                        var warningMsg = importResult.Warnings.Any() ? $"Avisos de Importação: {string.Join(" | ", importResult.Warnings)}" : null;

                        await _logService.WriteJobStatusAsync(
                            jobId: result.JobId,
                            userId: result.UserId,
                            status: statusStr,
                            store: result.Store ?? "Unknown",
                            matricula: result.Matricula ?? "Unknown",
                            runId: result.RunId,
                            additionalData: new 
                            { 
                                ErrorMessage = errorMsg,
                                WarningMessage = warningMsg
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    // Update log with import error
                    await _logService.WriteJobStatusAsync(
                        jobId: result.JobId,
                        userId: result.UserId,
                        status: "Failed",
                        store: result.Store ?? "Unknown",
                        matricula: result.Matricula ?? "Unknown",
                        runId: result.RunId,
                        additionalData: new { ErrorMessage = $"Erro no sistema de importação: {ex.Message}" }
                    );
                }
            }
        }
    }
}
