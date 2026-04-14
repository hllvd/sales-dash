using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IScrapeOrchestrator
    {
        Task<string> TriggerScrapeAsync(int configId, bool isManual = true);
        Task HandleCallbackAsync(string jobId, string status, string? fileRelativePath, int rowCount, string? error);
    }

    public class ScrapeOrchestrator : IScrapeOrchestrator
    {
        private readonly AppDbContext _context;
        private readonly PbiScraperClient _scraperClient;
        private readonly IScrapeDynamoLogService _logService;
        private readonly IScrapeImportService _importService;
        private readonly string _outputDir;

        public ScrapeOrchestrator(
            AppDbContext context,
            PbiScraperClient scraperClient,
            IScrapeDynamoLogService logService,
            IScrapeImportService importService,
            IConfiguration configuration)
        {
            _context = context;
            _scraperClient = scraperClient;
            _logService = logService;
            _importService = importService;
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
        }

        public async Task<string> TriggerScrapeAsync(int configId, bool isManual = true)
        {
            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == configId);

            if (config == null) throw new ArgumentException($"Config {configId} not found");

            var jobId = Guid.NewGuid().ToString();
            
            // 1. Log start in DynamoDB
            await _logService.WriteJobStatusAsync(
                jobId: jobId,
                userId: config.UserId.ToString(),
                status: "Pending",
                store: config.Store,
                matricula: config.Matricula,
                additionalData: new { IsManual = isManual }
            );

            // 2. Call Node.js service
            try
            {
                await _scraperClient.EnqueueJobAsync(jobId, config.UserId.ToString(), config.Store, config.Matricula);
                
                // Update status to Running
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId.ToString(),
                    status: "Running",
                    store: config.Store,
                    matricula: config.Matricula
                );
            }
            catch (Exception ex)
            {
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId.ToString(),
                    status: "Failed",
                    store: config.Store,
                    matricula: config.Matricula,
                    additionalData: new { ErrorMessage = ex.Message }
                );
                throw;
            }

            return jobId;
        }

        public async Task HandleCallbackAsync(string jobId, string status, string? fileRelativePath, int rowCount, string? error)
        {
            // Update DynamoDB with result
            // Note: We need the userId, store, and matricula to update the correct DynamoDB key.
            // In a real scenario, we'd either look up the job or pass this info in the callback.
            // For now, we'll use a GSI or search. 
            // Better: Scraper service returns everything it received.

            // 1. Get Job from DynamoDB to find UserId (needed for the PK)
            // Implementation detail: for now we'll assume the callback includes all info
            // but the simplified plan said "updates DynamoDB".
            
            // Let's assume HandleCallbackAsync is called from the controller with full info
            // provided by the scraper callback.
        }
    }
}
