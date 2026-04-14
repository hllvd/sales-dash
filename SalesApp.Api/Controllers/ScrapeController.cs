using Microsoft.AspNetCore.Mvc;
using SalesApp.Models;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScrapeController : ControllerBase
    {
        private readonly IScrapeOrchestrator _orchestrator;
        private readonly IScrapeDynamoLogService _logService;
        private readonly AppDbContext _context;
        private readonly IScrapeImportService _importService;
        private readonly string _outputDir;

        public ScrapeController(
            IScrapeOrchestrator orchestrator,
            IScrapeDynamoLogService logService,
            AppDbContext context,
            IScrapeImportService importService,
            IConfiguration configuration)
        {
            _orchestrator = orchestrator;
            _logService = logService;
            _context = context;
            _importService = importService;
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("configs")]
        public async Task<IActionResult> CreateConfig([FromBody] ScrapeConfig config)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            // Auto-fill UserId if not provided or if the user is not a superadmin
            if (!config.UserId.HasValue || config.UserId == Guid.Empty || !User.IsInRole("superadmin"))
            {
                config.UserId = userId;
            }

            // Check if user is admin but trying to create for someone else
            if (User.IsInRole("admin") && !User.IsInRole("superadmin") && config.UserId != userId)
            {
                return Forbid();
            }

            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            
            _context.ScrapeConfigs.Add(config);
            await _context.SaveChangesAsync();
            
            return Ok(config);
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("configs/me")]
        public async Task<IActionResult> GetMyConfigs()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var configs = await _context.ScrapeConfigs
                .Where(c => c.UserId == userId)
                .ToListAsync();
            return Ok(configs);
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("jobs/{configId}")]
        public async Task<IActionResult> TriggerScrape(int configId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var config = await _context.ScrapeConfigs.FindAsync(configId);

            if (config == null) return NotFound();
            if (User.IsInRole("admin") && !User.IsInRole("superadmin") && config.UserId != userId) return Forbid();

            var jobId = await _orchestrator.TriggerScrapeAsync(configId, isManual: true);
            return Accepted(new { jobId });
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("jobs/me")]
        public async Task<IActionResult> GetMyJobs()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var jobs = await _logService.GetJobsByUserAsync(userId);
            return Ok(jobs);
        }

        [Authorize(Roles = "superadmin")]
        [HttpGet("jobs/all")]
        public async Task<IActionResult> GetAllJobs()
        {
            var jobs = await _logService.GetAllJobsAsync();
            return Ok(jobs);
        }

        // Internal callback - No auth as per user request for simplicity
        [HttpPut("callback")]
        public async Task<IActionResult> HandleCallback([FromBody] ScrapeResult result)
        {
            // 1. Update DynamoDB status
            await _logService.WriteJobStatusAsync(
                jobId: result.JobId,
                userId: result.UserId,
                status: result.Status,
                store: result.Store ?? "Unknown",
                matricula: result.Matricula ?? "Unknown",
                additionalData: new
                {
                    RowCount = result.RowCount,
                    FileRelativePath = result.FileRelativePath,
                    ErrorMessage = result.Error,
                    CompletedAt = DateTime.UtcNow.ToString("O")
                }
            );

            // 2. Trigger Auto-Import if success
            if (result.Status == "Succeeded" && !string.IsNullOrEmpty(result.FileRelativePath))
            {
                var filePath = Path.Combine(_outputDir, result.FileRelativePath);
                try
                {
                    await _importService.AutoImportAsync(filePath, Guid.Parse(result.UserId));
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
                        additionalData: new { ImportErrorMessage = ex.Message }
                    );
                }
            }

            return Ok();
        }
    }

    public class ScrapeResult
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Store { get; set; }
        public string? Matricula { get; set; }
        public int RowCount { get; set; }
        public string? FileRelativePath { get; set; }
        public string? Error { get; set; }
    }
}
