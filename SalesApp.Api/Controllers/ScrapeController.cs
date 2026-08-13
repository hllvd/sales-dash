using Microsoft.AspNetCore.Mvc;
using SalesApp.Models;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SalesApp.Services;
using Microsoft.AspNetCore.DataProtection;

namespace SalesApp.Controllers
{
    public class ScrapeConfigDto
    {
        public int Id { get; set; }
        public Guid? UserId { get; set; }
        public string Store { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public string? CredentialStatus { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ScrapeConfigRequest
    {
        public int? Id { get; set; }
        public string Store { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public string? PowerBiPassword { get; set; }
        public bool TestOnSave { get; set; } = true;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ScrapeController : ControllerBase
    {
        private readonly IScrapeOrchestrator _orchestrator;
        private readonly IScrapeDynamoLogService _logService;
        private readonly AppDbContext _context;
        private readonly IScrapeImportService _importService;
        private readonly PbiScraperClient _scraperClient;
        private readonly IDataProtector _protector;
        private readonly string _outputDir;
        private readonly bool _isE2E;

        public ScrapeController(
            IScrapeOrchestrator orchestrator,
            IScrapeDynamoLogService logService,
            AppDbContext context,
            IScrapeImportService importService,
            PbiScraperClient scraperClient,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration)
        {
            _orchestrator = orchestrator;
            _logService = logService;
            _context = context;
            _importService = importService;
            _scraperClient = scraperClient;
            _protector = dataProtectionProvider.CreateProtector("ScrapeConfig.PowerBiPassword");
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
            _isE2E = configuration["ASPNETCORE_ENVIRONMENT"] == "E2E";
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("configs")]
        public async Task<IActionResult> SaveConfig([FromBody] ScrapeConfigRequest request)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            ScrapeConfig? config;
            bool isNew = false;

            if (request.Id.HasValue && request.Id > 0)
            {
                config = await _context.ScrapeConfigs
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == request.Id.Value);
                if (config == null) return NotFound();
                if (!User.IsInRole("superadmin") && config.UserId != userId) return Forbid();
            }
            else
            {
                config = new ScrapeConfig
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                isNew = true;
            }

            config.Store = request.Store;
            config.Matricula = request.Matricula;
            config.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.PowerBiPassword))
            {
                config.PowerBiPassword = request.PowerBiPassword;
                config.CredentialStatus = null; // Reset status on password change

                // Test authentication if requested and not in E2E
                if (request.TestOnSave && !_isE2E)
                {
                    var (success, loginSuccess, message, steps) = await _scraperClient.TestAuthAsync(request.Matricula, request.PowerBiPassword);
                    config.CredentialStatus = (loginSuccess || success) ? "ok" : "wrong-password";
                    if (!loginSuccess && !success)
                    {
                        return BadRequest(new { message = $"Falha na autenticação: {message}", steps });
                    }
                }
            }

            if (isNew) _context.ScrapeConfigs.Add(config);
            await _context.SaveChangesAsync();
            
            return Ok(MapToDto(config));
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("configs/me")]
        public async Task<IActionResult> GetMyConfigs()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var configs = await _context.ScrapeConfigs
                .Where(c => c.User.Id == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            
            return Ok(configs.Select(MapToDto));
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpDelete("configs/{id}")]
        public async Task<IActionResult> DeleteConfig(int id)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config == null) return NotFound();
            if (!User.IsInRole("superadmin") && config.UserId != userId) return Forbid();

            _context.ScrapeConfigs.Remove(config);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("configs/{id}/test-auth")]
        public async Task<IActionResult> TestAuth(int id, [FromQuery] bool force = false)
        {
            if (_isE2E) return Ok(new { success = true, message = "Autenticação ignorada em modo E2E", steps = new string[0] });

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config == null) return NotFound();
            if (!User.IsInRole("superadmin") && config.UserId != userId) return Forbid();

            if (string.IsNullOrEmpty(config.PowerBiPassword))
            {
                return BadRequest(new { message = "Senha não configurada" });
            }

            if (config.CredentialStatus == "wrong-password" && !force)
            {
                var warnMessage = "Já testamos essas credenciais recentemente e ocorreu um erro de senha. Tem certeza que deseja testar novamente?";
                return Ok(new { 
                    success = false, 
                    requiresConfirmation = true,
                    message = warnMessage, 
                    steps = new[] { $"[Aviso] Teste prevenido para evitar bloqueio de conta: {warnMessage}" } 
                });
            }

            string password = config.PowerBiPassword;

            var (success, loginSuccess, message, steps) = await _scraperClient.TestAuthAsync(config.Matricula, password);

            bool effectiveSuccess = loginSuccess || success;
            config.CredentialStatus = effectiveSuccess ? "ok" : "wrong-password";
            config.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = effectiveSuccess, loginSuccess, message, steps, credentialStatus = config.CredentialStatus });
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("jobs/{configId}")]
        public async Task<IActionResult> TriggerScrape(int configId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value 
                         ?? User.FindFirst("email")?.Value 
                         ?? User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                var dbUser = await _context.Users.FindAsync(userId);
                userEmail = dbUser?.Email ?? string.Empty;
            }

            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == configId);

            if (config == null) return NotFound();
            if (!User.IsInRole("admin") && !User.IsInRole("superadmin") && config.UserId != userId) return Forbid();

            var runId = Guid.NewGuid().ToString();
            var jobId = await _orchestrator.TriggerScrapeAsync(configId, isManual: true, runId: runId, userEmail: userEmail);
            return Accepted(new { jobId, runId });
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

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("runs/me")]
        public async Task<IActionResult> GetMyRuns()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var runs = await _logService.GetRunsByUserAsync(userId);
            return Ok(runs);
        }

        [Authorize(Roles = "superadmin")]
        [HttpGet("runs/all")]
        public async Task<IActionResult> GetAllRuns()
        {
            var runs = await _logService.GetAllRunsAsync();
            return Ok(runs);
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("runs/{runId}")]
        public async Task<IActionResult> GetRunDetail(string runId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var detail = await _logService.GetRunDetailAsync(runId, userId);
            if (detail == null && User.IsInRole("superadmin"))
            {
                detail = await _logService.GetRunDetailAsync(runId, null);
            }
            if (detail == null) return NotFound();

            if (!User.IsInRole("superadmin") && detail.UserId != userId.ToString())
            {
                return Forbid();
            }

            return Ok(detail);
        }

        [HttpPut("callback")]
        public async Task<IActionResult> HandleCallback([FromBody] ScrapeResult result)
        {
            await _orchestrator.HandleCallbackAsync(result);
            return Ok();
        }

        private static ScrapeConfigDto MapToDto(ScrapeConfig config)
        {
            return new ScrapeConfigDto
            {
                Id = config.Id,
                UserId = config.UserId,
                Store = config.Store,
                Matricula = config.Matricula,
                CredentialStatus = config.CredentialStatus,
                IsEnabled = config.IsEnabled,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
        }
    }
}
