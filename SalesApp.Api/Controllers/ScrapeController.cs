using Microsoft.AspNetCore.Mvc;
using SalesApp.Models;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SalesApp.Services;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace SalesApp.Controllers
{
    public class ScrapeConfigDto
    {
        public int Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OwnerUserId { get; set; }
        public string? OwnerUserName { get; set; }
        public string? OwnerUserEmail { get; set; }
        public string? Store { get; set; }
        public string Matricula { get; set; } = string.Empty;
        public string? CredentialStatus { get; set; }
        public string? DefaultStartMonth { get; set; }
        public string ScrapeType { get; set; } = "geral";
        public string OutputMode { get; set; } = "direct";
        public bool AutoImportSqs { get; set; } = true;
        public string? ScheduleMode { get; set; }
        public int? ScheduleIntervalHours { get; set; }
        public string? ScheduleTimes { get; set; }
        public int? ScrapeIntervalHours { get; set; }
        public DateTime? LastTriggeredAt { get; set; }
        public bool SkipMissingContractNumber { get; set; } = true;
        public bool AllowAutoCreateGroups { get; set; } = true;
        public bool AllowAutoCreatePVs { get; set; } = true;
        public bool UpdateMatriculaOnExisting { get; set; } = false;
        public bool UpdateTotalAmountOnExisting { get; set; } = true;
        public bool UpdateStartDateOnExisting { get; set; } = true;
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ScrapeConfigRequest
    {
        public int? Id { get; set; }
        public string? Store { get; set; }
        public string Matricula { get; set; } = string.Empty;
        public string? PowerBiPassword { get; set; }
        public string? DefaultStartMonth { get; set; }
        public string? ScrapeType { get; set; }
        public string? OutputMode { get; set; }
        public bool? AutoImportSqs { get; set; }
        public string? ScheduleMode { get; set; }
        public int? ScheduleIntervalHours { get; set; }
        public string? ScheduleTimes { get; set; }
        public int? ScrapeIntervalHours { get; set; }
        public bool? SkipMissingContractNumber { get; set; }
        public bool? AllowAutoCreateGroups { get; set; }
        public bool? AllowAutoCreatePVs { get; set; }
        public bool? UpdateMatriculaOnExisting { get; set; }
        public bool? UpdateTotalAmountOnExisting { get; set; }
        public bool? UpdateStartDateOnExisting { get; set; }
        public Guid? OwnerUserId { get; set; }
        public bool TestOnSave { get; set; } = true;
    }

    public class TriggerScrapeJobRequest
    {
        public string? StartMonth { get; set; }
        public int MonthsCount { get; set; } = 3;
        public string? ScrapeType { get; set; }
    }

    public class ImportFromS3Request
    {
        public string S3Key { get; set; } = string.Empty;
        public string S3Bucket { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? JobId { get; set; }
        public string? RunId { get; set; }
        public string? Matricula { get; set; }
    }

    public class ScrapeDownloadLinkDto
    {
        public string JobId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
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
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScrapeController> _logger;
        private readonly IAmazonS3? _s3;
        private readonly string _outputDir;
        private readonly bool _isE2E;

        public ScrapeController(
            IScrapeOrchestrator orchestrator,
            IScrapeDynamoLogService logService,
            AppDbContext context,
            IScrapeImportService importService,
            PbiScraperClient scraperClient,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            ILogger<ScrapeController> logger,
            IAmazonS3? s3 = null)
        {
            _orchestrator = orchestrator;
            _logService = logService;
            _context = context;
            _importService = importService;
            _scraperClient = scraperClient;
            _protector = dataProtectionProvider.CreateProtector("ScrapeConfig.PowerBiPassword");
            _configuration = configuration;
            _logger = logger;
            _s3 = s3;
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
            _isE2E = configuration["ASPNETCORE_ENVIRONMENT"] == "E2E";
        }

        private bool IsSuperAdmin()
        {
            return User.IsInRole("superadmin")
                || User.IsInRole("SuperAdmin")
                || User.HasClaim("perm", "system:superadmin")
                || User.FindFirst("role_id")?.Value == "1";
        }

        private bool CanAccessScrapes(User user)
        {
            return IsSuperAdmin()
                || user.CanAccessPowerBiScrape
                || User.HasClaim("perm", "scrapes:access");
        }

        [Authorize(Roles = "superadmin")]
        [HttpGet("eligible-owners")]
        public async Task<IActionResult> GetEligibleOwners()
        {
            var eligibleAdmins = await _context.Users
                .Where(u => u.IsActive && u.RoleId == 2 && u.CanAccessPowerBiScrape)
                .OrderBy(u => u.Name)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.Name,
                    email = u.Email
                })
                .ToListAsync();

            return Ok(eligibleAdmins);
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("configs")]
        public async Task<IActionResult> SaveConfig([FromBody] ScrapeConfigRequest request)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            if (string.IsNullOrWhiteSpace(request.Matricula))
            {
                return BadRequest(new { message = "Matrícula é obrigatória." });
            }

            ScrapeConfig? config;
            bool isNew = false;

            if (request.Id.HasValue && request.Id > 0)
            {
                config = await _context.ScrapeConfigs
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == request.Id.Value);
                if (config == null) return NotFound();
                if (!IsSuperAdmin() && config.UserInternalId != user.InternalId) return Forbid();
            }
            else
            {
                if (!IsSuperAdmin())
                {
                    return Forbid();
                }

                if (string.IsNullOrWhiteSpace(request.PowerBiPassword))
                {
                    return BadRequest(new { message = "Senha é obrigatória para novas credenciais." });
                }

                config = new ScrapeConfig
                {
                    UserInternalId = user.InternalId,
                    User = user,
                    CreatedAt = DateTime.UtcNow
                };
                isNew = true;
            }

            if (IsSuperAdmin() && request.OwnerUserId.HasValue)
            {
                var ownerUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == request.OwnerUserId.Value && u.IsActive);

                if (ownerUser == null || (ownerUser.RoleId != 1 && !(ownerUser.RoleId == 2 && ownerUser.CanAccessPowerBiScrape)))
                {
                    return BadRequest(new { message = "O responsável selecionado deve ser um Admin ativo com permissão de acesso ao PowerBI habilitada." });
                }

                config.UserInternalId = ownerUser.InternalId;
                config.User = ownerUser;
                config.UserId = ownerUser.Id;
            }

            var cleanStore = request.Store?.Trim();
            if (string.Equals(cleanStore, "AUTO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanStore, "Tentar selecionar automaticamente", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(cleanStore))
            {
                config.Store = null;
            }
            else
            {
                config.Store = cleanStore;
            }

            config.Matricula = request.Matricula.Trim();
            config.DefaultStartMonth = string.IsNullOrWhiteSpace(request.DefaultStartMonth) ? null : request.DefaultStartMonth.Trim();
            config.ScrapeType = string.IsNullOrWhiteSpace(request.ScrapeType) ? "geral" : request.ScrapeType.Trim().ToLowerInvariant();
            config.OutputMode = string.IsNullOrWhiteSpace(request.OutputMode) ? "direct" : request.OutputMode.Trim().ToLowerInvariant();
            if (request.AutoImportSqs.HasValue) config.AutoImportSqs = request.AutoImportSqs.Value;

            var cleanScheduleMode = string.IsNullOrWhiteSpace(request.ScheduleMode) ? null : request.ScheduleMode.Trim().ToLowerInvariant();
            if (cleanScheduleMode == "disabled" || cleanScheduleMode == "0" || cleanScheduleMode == "none")
            {
                cleanScheduleMode = null;
            }

            config.ScheduleMode = cleanScheduleMode;
            config.ScheduleIntervalHours = request.ScheduleIntervalHours;
            config.ScheduleTimes = string.IsNullOrWhiteSpace(request.ScheduleTimes) ? null : request.ScheduleTimes.Trim();

            if (config.ScheduleMode == "interval")
            {
                var intervalVal = config.ScheduleIntervalHours ?? request.ScrapeIntervalHours;
                if (intervalVal.HasValue && intervalVal.Value < 1)
                {
                    return BadRequest(new { message = "O intervalo mínimo de extração automática é de 1 hora." });
                }
                config.ScheduleIntervalHours = intervalVal;
                config.ScrapeIntervalHours = intervalVal;
                config.ScheduleTimes = null;
            }
            else if (config.ScheduleMode == "daily")
            {
                if (string.IsNullOrWhiteSpace(config.ScheduleTimes) || config.ScheduleTimes == "[]")
                {
                    return BadRequest(new { message = "Selecione ao menos um horário para a extração diária." });
                }
                config.ScheduleIntervalHours = null;
                config.ScrapeIntervalHours = null;
            }
            else if (config.ScheduleMode == "weekly")
            {
                if (string.IsNullOrWhiteSpace(config.ScheduleTimes) || config.ScheduleTimes == "{}")
                {
                    return BadRequest(new { message = "Selecione o dia e ao menos um horário para a extração semanal." });
                }
                config.ScheduleIntervalHours = null;
                config.ScrapeIntervalHours = null;
            }
            else
            {
                if (request.ScrapeIntervalHours.HasValue && request.ScrapeIntervalHours.Value >= 1)
                {
                    config.ScheduleMode = "interval";
                    config.ScheduleIntervalHours = request.ScrapeIntervalHours.Value;
                    config.ScrapeIntervalHours = request.ScrapeIntervalHours.Value;
                }
                else
                {
                    config.ScheduleMode = null;
                    config.ScheduleIntervalHours = null;
                    config.ScheduleTimes = null;
                    config.ScrapeIntervalHours = null;
                }
            }

            if (request.SkipMissingContractNumber.HasValue) config.SkipMissingContractNumber = request.SkipMissingContractNumber.Value;
            if (request.AllowAutoCreateGroups.HasValue) config.AllowAutoCreateGroups = request.AllowAutoCreateGroups.Value;
            if (request.AllowAutoCreatePVs.HasValue) config.AllowAutoCreatePVs = request.AllowAutoCreatePVs.Value;
            if (request.UpdateMatriculaOnExisting.HasValue) config.UpdateMatriculaOnExisting = request.UpdateMatriculaOnExisting.Value;
            if (request.UpdateTotalAmountOnExisting.HasValue) config.UpdateTotalAmountOnExisting = request.UpdateTotalAmountOnExisting.Value;
            if (request.UpdateStartDateOnExisting.HasValue) config.UpdateStartDateOnExisting = request.UpdateStartDateOnExisting.Value;
            config.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.PowerBiPassword))
            {
                config.PowerBiPassword = request.PowerBiPassword;
                config.CredentialStatus = null; // Reset status on password change
            }

            // Test authentication if requested
            if (request.TestOnSave)
            {
                var passwordToTest = !string.IsNullOrEmpty(request.PowerBiPassword)
                    ? request.PowerBiPassword
                    : config.PowerBiPassword;

                if (!string.IsNullOrEmpty(passwordToTest))
                {
                    if (_isE2E && config.Matricula == "789012")
                    {
                        config.CredentialStatus = "ok";
                        if (string.IsNullOrEmpty(config.Store))
                        {
                            config.Store = "AHU - PR";
                        }
                        if (string.IsNullOrWhiteSpace(config.DefaultStartMonth))
                        {
                            config.DefaultStartMonth = DateTime.UtcNow.ToString("yyyy-MM");
                        }
                    }
                    else
                    {
                        try
                        {
                            var (success, loginSuccess, message, steps, detectedStore, detectedStartDate) = await _scraperClient.TestAuthAsync(config.Matricula, passwordToTest, config.Store);
                            config.CredentialStatus = success ? "ok" : (loginSuccess == false ? "wrong-password" : "error");
                            if (string.IsNullOrEmpty(config.Store) && !string.IsNullOrEmpty(detectedStore))
                            {
                                config.Store = detectedStore;
                            }
                            if (string.IsNullOrWhiteSpace(config.DefaultStartMonth) && !string.IsNullOrWhiteSpace(detectedStartDate))
                            {
                                config.DefaultStartMonth = detectedStartDate;
                            }

                            if (!success)
                            {
                                var isTimeout = message != null && (message.Contains("Tempo limite", StringComparison.OrdinalIgnoreCase) || message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
                                var errorType = isTimeout ? "timeout" : (loginSuccess == false ? "wrong-password" : "auth-error");
                                var detailedMessage = isTimeout
                                    ? $"Timeout ao validar senha no AVA PRO: {message}"
                                    : (loginSuccess == false
                                        ? $"Senha recusada pelo AVA PRO: {message}"
                                        : $"Erro técnico ao validar acesso no AVA PRO: {message}");

                                return BadRequest(new
                                {
                                    message = detailedMessage,
                                    errorType,
                                    steps,
                                    credentialStatus = config.CredentialStatus
                                });
                            }
                        }
                        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                        {
                            _logger.LogWarning(ex, "Serviço HTTP do scraper inacessível para testar credenciais durante SaveConfig. Configuração salva sem validação ao vivo.");
                            config.CredentialStatus = null;
                        }
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            IQueryable<ScrapeConfig> query = _context.ScrapeConfigs
                .Include(c => c.User)
                .Where(c => c.OutputMode != null && c.OutputMode.ToLower() == "sqs");
            if (!IsSuperAdmin())
            {
                query = query.Where(c => c.UserInternalId == user.InternalId);
            }

            var configs = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            
            return Ok(configs.Select(MapToDto));
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpDelete("configs/{id}")]
        public async Task<IActionResult> DeleteConfig(int id)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!IsSuperAdmin()) return Forbid();

            var config = await _context.ScrapeConfigs
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config == null) return NotFound();

            _context.ScrapeConfigs.Remove(config);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("configs/{id}/test-auth")]
        public async Task<IActionResult> TestAuth(int id, [FromQuery] bool force = false)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config == null) return NotFound();
            if (!IsSuperAdmin() && config.UserInternalId != user.InternalId && config.UserId != userId) return Forbid();

            if (string.IsNullOrEmpty(config.PowerBiPassword))
            {
                return BadRequest(new { message = "Senha não configurada" });
            }

            if (_isE2E && config.Matricula == "789012")
            {
                config.CredentialStatus = "ok";
                if (string.IsNullOrEmpty(config.Store)) config.Store = "AHU - PR";
                if (string.IsNullOrWhiteSpace(config.DefaultStartMonth)) config.DefaultStartMonth = DateTime.UtcNow.ToString("yyyy-MM");
                await _context.SaveChangesAsync();
                return Ok(new { success = true, loginSuccess = true, message = "Autenticação em modo E2E", steps = new string[0], detectedStore = config.Store, detectedStartDate = config.DefaultStartMonth });
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

            try
            {
                var (success, loginSuccess, message, steps, detectedStore, detectedStartDate) = await _scraperClient.TestAuthAsync(config.Matricula, password, config.Store);

                config.CredentialStatus = success ? "ok" : (loginSuccess == false ? "wrong-password" : "error");
                if (string.IsNullOrEmpty(config.Store) && !string.IsNullOrEmpty(detectedStore))
                {
                    config.Store = detectedStore;
                }
                if (string.IsNullOrWhiteSpace(config.DefaultStartMonth) && !string.IsNullOrWhiteSpace(detectedStartDate))
                {
                    config.DefaultStartMonth = detectedStartDate;
                }
                config.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success, loginSuccess, message, steps, credentialStatus = config.CredentialStatus, detectedStore, detectedStartDate });
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "Serviço HTTP do scraper inacessível para TestAuth.");
                return StatusCode(503, new { 
                    success = false, 
                    loginSuccess = false, 
                    message = "Serviço de teste do scraper não está acessível no momento.", 
                    steps = new[] { "[Erro] Não foi possível conectar ao scraper HTTP para testar a senha em tempo real." },
                    credentialStatus = config.CredentialStatus 
                });
            }
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpPost("jobs/{configId}")]
        public async Task<IActionResult> TriggerScrape(int configId, [FromBody] TriggerScrapeJobRequest? request = null)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (currentUser == null) return Unauthorized();
            if (!CanAccessScrapes(currentUser)) return Forbid();

            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == configId);

            if (config == null) return NotFound();
            if (!IsSuperAdmin() && config.UserInternalId != currentUser.InternalId && config.UserId != userId) return Forbid();

            var ownerEmail = config.User?.Email;
            if (string.IsNullOrEmpty(ownerEmail) && config.UserInternalId.HasValue)
            {
                var ownerUser = await _context.Users.FirstOrDefaultAsync(u => u.InternalId == config.UserInternalId.Value);
                ownerEmail = ownerUser?.Email;
            }
            ownerEmail ??= currentUser.Email;

            if (config.CredentialStatus == "wrong-password")
            {
                return BadRequest(new { 
                    message = "Credenciais inválidas ('wrong-password'). A extração não foi iniciada para proteger sua conta contra bloqueio no AVA PRO. Atualize e teste sua senha antes de solicitar novas extrações." 
                });
            }

            if (!_isE2E && !string.IsNullOrEmpty(config.PowerBiPassword))
            {
                try
                {
                    var (success, loginSuccess, authMessage, steps, detectedStore, _) =
                        await _scraperClient.TestAuthAsync(config.Matricula, config.PowerBiPassword, config.Store, useCache: true);

                    config.CredentialStatus = success ? "ok" : (loginSuccess == false ? "wrong-password" : "error");
                    if (string.IsNullOrEmpty(config.Store) && !string.IsNullOrEmpty(detectedStore))
                    {
                        config.Store = detectedStore;
                    }
                    config.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    if (!success)
                    {
                        var isTimeout = authMessage != null && (authMessage.Contains("Tempo limite", StringComparison.OrdinalIgnoreCase) || authMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase));
                        var errorType = isTimeout ? "timeout" : (loginSuccess == false ? "wrong-password" : "auth-error");
                        var detailedMessage = isTimeout
                            ? $"Timeout ao validar senha no AVA PRO: {authMessage}"
                            : (loginSuccess == false
                                ? $"Senha recusada pelo AVA PRO: {authMessage}"
                                : $"Erro técnico ao validar acesso no AVA PRO: {authMessage}");

                        return BadRequest(new
                        {
                            message = detailedMessage,
                            errorType,
                            steps,
                            credentialStatus = config.CredentialStatus
                        });
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    _logger.LogWarning(ex, "Serviço HTTP local do scraper inacessível durante pré-validação de TriggerScrape para matrícula {Matricula}. Prosseguindo com enfileiramento.", config.Matricula);
                }
            }

            var runId = Guid.NewGuid().ToString();
            var monthsCount = request?.MonthsCount > 0 ? request.MonthsCount : 3;
            var effectiveStartMonth = !string.IsNullOrWhiteSpace(request?.StartMonth) ? request.StartMonth : config.DefaultStartMonth;
            var effectiveScrapeType = !string.IsNullOrWhiteSpace(request?.ScrapeType) ? request.ScrapeType : config.ScrapeType;
            var maxMonthsAgo = _configuration.GetValue<int>("PbiScraper:MaxMonthsAgo", 15);
            var scrapeDates = CalculateScrapeDates(effectiveStartMonth, monthsCount, maxMonthsAgo);

            var jobIds = new List<string>();

            // For consultor scrapeType, all requested months can be executed in a single consolidated semantic query
            if (string.Equals(effectiveScrapeType, "consultor", StringComparison.OrdinalIgnoreCase) && scrapeDates.Count > 1 && scrapeDates.All(d => !string.IsNullOrEmpty(d)))
            {
                var consolidatedDate = string.Join(",", scrapeDates);
                var jobId = await _orchestrator.TriggerScrapeAsync(configId, isManual: true, runId: runId, userEmail: ownerEmail, scrapeDate: consolidatedDate, scrapeType: effectiveScrapeType);
                jobIds.Add(jobId);
            }
            else
            {
                foreach (var date in scrapeDates)
                {
                    var jobId = await _orchestrator.TriggerScrapeAsync(configId, isManual: true, runId: runId, userEmail: ownerEmail, scrapeDate: date, scrapeType: effectiveScrapeType);
                    jobIds.Add(jobId);
                }
            }

            return Accepted(new { jobId = jobIds.FirstOrDefault(), jobIds, runId, scrapeDates });
        }

        private static List<string?> CalculateScrapeDates(string? startMonth, int defaultCount = 3, int maxMonthsAgo = 15)
        {
            var dates = new List<string?>();
            var now = DateTime.UtcNow;
            var currentYearMonth = new DateTime(now.Year, now.Month, 1);
            var effectiveMaxMonthsAgo = maxMonthsAgo > 0 ? maxMonthsAgo : 15;
            var earliestAllowedMonth = currentYearMonth.AddMonths(-effectiveMaxMonthsAgo);

            if (!string.IsNullOrWhiteSpace(startMonth) && DateTime.TryParseExact(startMonth.Trim(), "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startParsed))
            {
                var cursor = new DateTime(startParsed.Year, startParsed.Month, 1);

                if (cursor < earliestAllowedMonth)
                {
                    cursor = earliestAllowedMonth;
                }

                if (cursor > currentYearMonth)
                {
                    dates.Add(currentYearMonth.ToString("yyyy-MM"));
                }
                else
                {
                    while (cursor <= currentYearMonth)
                    {
                        dates.Add(cursor.ToString("yyyy-MM"));
                        cursor = cursor.AddMonths(1);
                    }
                }
            }
            else
            {
                // If user does not set a start month, do not apply any date filter in PowerBI
                dates.Add(null);
            }

            return dates;
        }

        [Authorize(Roles = "admin,superadmin")]
        [HttpGet("jobs/me")]
        public async Task<IActionResult> GetMyJobs()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            var jobs = IsSuperAdmin()
                ? await _logService.GetAllJobsAsync()
                : await _logService.GetJobsByUserAsync(userId);
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            var runs = IsSuperAdmin()
                ? await _logService.GetAllRunsAsync()
                : await _logService.GetRunsByUserAsync(userId);
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (!CanAccessScrapes(user)) return Forbid();

            var detail = await _logService.GetRunDetailAsync(runId, userId);
            if (detail == null && IsSuperAdmin())
            {
                detail = await _logService.GetRunDetailAsync(runId, null);
            }
            if (detail == null) return NotFound();

            if (!IsSuperAdmin() && detail.UserId != userId.ToString())
            {
                return Forbid();
            }

            return Ok(detail);
        }

        public static List<ScrapeLogEntry> SelectDownloadableJobs(IEnumerable<ScrapeLogEntry> jobs)
        {
            return jobs
                .Where(j => string.Equals(j.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(j.S3Bucket)
                    && !string.IsNullOrWhiteSpace(j.S3Key))
                .OrderByDescending(j => j.CreatedAt)
                .ToList();
        }

        public static string BuildDownloadFileName(ScrapeLogEntry job)
        {
            var keyFileName = !string.IsNullOrWhiteSpace(job.S3Key) ? Path.GetFileName(job.S3Key) : null;
            if (!string.IsNullOrWhiteSpace(keyFileName))
            {
                return keyFileName;
            }
            var matricula = string.IsNullOrWhiteSpace(job.Matricula) ? "scrape" : job.Matricula;
            return $"scrape_{matricula}_{job.JobId}.csv";
        }

        [Authorize(Roles = "superadmin")]
        [HttpGet("runs/{runId}/download")]
        public async Task<IActionResult> DownloadRunFiles(string runId)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                return BadRequest(new { message = "RunId é obrigatório." });
            }

            if (_s3 == null)
            {
                return StatusCode(503, new { message = "Serviço S3 indisponível." });
            }

            var detail = await _logService.GetRunDetailAsync(runId, null);
            if (detail == null)
            {
                return NotFound(new { message = "Execução não encontrada." });
            }

            var downloadableJobs = SelectDownloadableJobs(detail.Jobs);
            if (!downloadableJobs.Any())
            {
                return NotFound(new { message = "Nenhum arquivo S3 disponível para download nesta execução." });
            }

            var actorEmail = User?.FindFirst(ClaimTypes.Email)?.Value
                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                ?? "superadmin";

            var links = new List<ScrapeDownloadLinkDto>();
            foreach (var job in downloadableJobs)
            {
                try
                {
                    await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    {
                        BucketName = job.S3Bucket,
                        Key = job.S3Key
                    });
                }
                catch (AmazonS3Exception s3Ex) when (
                    s3Ex.StatusCode == HttpStatusCode.NotFound
                    || string.Equals(s3Ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s3Ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "S3 object missing for RunId={RunId}, JobId={JobId}, Bucket={Bucket}, Key={Key}",
                        runId, job.JobId, job.S3Bucket, job.S3Key);
                    continue;
                }

                var fileName = BuildDownloadFileName(job);
                var expiresAt = DateTime.UtcNow.AddMinutes(5);
                var presignedUrl = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
                {
                    BucketName = job.S3Bucket,
                    Key = job.S3Key,
                    Verb = HttpVerb.GET,
                    Expires = expiresAt,
                    ResponseHeaderOverrides = new ResponseHeaderOverrides
                    {
                        ContentDisposition = $"attachment; filename=\"{fileName}\""
                    }
                });

                _logger.LogInformation(
                    "AUDIT: Superadmin {UserEmail} generated S3 presigned download URL for RunId={RunId}, JobId={JobId}, Bucket={Bucket}, Key={Key}, ExpiresAt={ExpiresAt:O}",
                    actorEmail, runId, job.JobId, job.S3Bucket, job.S3Key, expiresAt);

                links.Add(new ScrapeDownloadLinkDto
                {
                    JobId = job.JobId,
                    FileName = fileName,
                    Url = presignedUrl,
                    ExpiresAt = expiresAt
                });
            }

            if (!links.Any())
            {
                return NotFound(new { message = "Arquivo não está mais disponível no S3." });
            }

            return Ok(links);
        }

        [Authorize(Roles = "superadmin")]
        [HttpPost("runs/{runId}/cancel")]
        public async Task<IActionResult> CancelRun(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                return BadRequest(new { message = "RunId is required." });
            }

            var cancelledByEmail = User?.FindFirst(ClaimTypes.Email)?.Value
                                ?? User?.FindFirst("email")?.Value
                                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                ?? "superadmin";

            var cancelledCount = await _orchestrator.CancelRunAsync(runId.Trim(), cancelledByEmail);
            if (cancelledCount == 0)
            {
                return NotFound(new { message = "Nenhum job ativo encontrado para cancelar nesta execução." });
            }

            return Ok(new { success = true, cancelledCount, runId });
        }

        [Authorize(Roles = "superadmin")]
        [HttpPost("jobs/{jobId}/cancel")]
        public async Task<IActionResult> CancelJob(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { message = "JobId is required." });
            }

            var cancelledByEmail = User?.FindFirst(ClaimTypes.Email)?.Value
                                ?? User?.FindFirst("email")?.Value
                                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                ?? "superadmin";

            var cancelled = await _orchestrator.CancelJobAsync(jobId.Trim(), cancelledByEmail);
            if (!cancelled)
            {
                return NotFound(new { message = "Job ativo não encontrado ou já finalizado." });
            }

            return Ok(new { success = true, jobId });
        }

        [HttpPut("callback")]
        public async Task<IActionResult> HandleCallback([FromBody] ScrapeResult? result)
        {
            if (!HasValidWorkerSecret())
            {
                _logger.LogWarning("Rejected unauthorized PUT /api/scrape/callback request from {RemoteIp}.", HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown");
                return Unauthorized(new { message = "Invalid or missing worker secret." });
            }

            if (result == null || string.IsNullOrWhiteSpace(result.JobId))
            {
                return BadRequest(new { message = "JobId is required." });
            }

            await _orchestrator.HandleCallbackAsync(result);
            return Ok();
        }

        [HttpPost("import-from-s3")]
        public async Task<IActionResult> ImportFromS3([FromBody] ImportFromS3Request request)
        {
            if (!HasValidWorkerSecret())
            {
                _logger.LogWarning("Rejected unauthorized POST /api/scrape/import-from-s3 request from {RemoteIp}.", HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown");
                return Unauthorized(new { message = "Invalid or missing worker secret." });
            }

            if (string.IsNullOrEmpty(request.S3Key) || string.IsNullOrEmpty(request.S3Bucket))
                return BadRequest(new { message = "S3Key and S3Bucket are required." });

            try
            {
                Guid? userId = null;
                if (!string.IsNullOrEmpty(request.UserId) && Guid.TryParse(request.UserId, out Guid parsedUid))
                {
                    userId = parsedUid;
                }

                var importResult = await _importService.AutoImportFromS3Async(
                    request.S3Bucket,
                    request.S3Key,
                    userId
                );

                return Ok(new
                {
                    success = !importResult.Errors.Any(),
                    importedCount = importResult.ProcessedRows,
                    warnings = importResult.Warnings,
                    errors = importResult.Errors
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Import failed: {ex.Message}" });
            }
        }

        private bool HasValidWorkerSecret()
        {
            var expectedSecret = _configuration["PbiScraper:WorkerSecret"]
                              ?? _configuration["WORKER_SECRET"]
                              ?? Environment.GetEnvironmentVariable("WORKER_SECRET");
            var providedSecret = Request?.Headers["X-Worker-Secret"].FirstOrDefault();
            return IsWorkerSecretValid(expectedSecret, providedSecret);
        }

        /// <summary>
        /// Fail-closed constant-time comparison of the shared X-Worker-Secret header.
        /// Rejects empty/unconfigured secrets to ensure internal scrape endpoints cannot be abused.
        /// </summary>
        public static bool IsWorkerSecretValid(string? expectedSecret, string? providedSecret)
        {
            if (string.IsNullOrWhiteSpace(expectedSecret) || string.IsNullOrWhiteSpace(providedSecret))
            {
                return false;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret.Trim());
            var providedBytes = Encoding.UTF8.GetBytes(providedSecret.Trim());
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }

        private static ScrapeConfigDto MapToDto(ScrapeConfig config)
        {
            return new ScrapeConfigDto
            {
                Id = config.Id,
                UserId = config.UserId,
                OwnerUserId = config.User?.Id ?? config.UserId,
                OwnerUserName = config.User?.Name,
                OwnerUserEmail = config.User?.Email,
                Store = config.Store,
                Matricula = config.Matricula,
                CredentialStatus = config.CredentialStatus,
                DefaultStartMonth = config.DefaultStartMonth,
                ScrapeType = config.ScrapeType ?? "geral",
                OutputMode = config.OutputMode ?? "direct",
                AutoImportSqs = config.AutoImportSqs,
                ScheduleMode = config.ScheduleMode,
                ScheduleIntervalHours = config.ScheduleIntervalHours ?? config.ScrapeIntervalHours,
                ScheduleTimes = config.ScheduleTimes,
                ScrapeIntervalHours = config.ScheduleIntervalHours ?? config.ScrapeIntervalHours,
                LastTriggeredAt = config.LastTriggeredAt,
                SkipMissingContractNumber = config.SkipMissingContractNumber,
                AllowAutoCreateGroups = config.AllowAutoCreateGroups,
                AllowAutoCreatePVs = config.AllowAutoCreatePVs,
                UpdateMatriculaOnExisting = config.UpdateMatriculaOnExisting,
                UpdateTotalAmountOnExisting = config.UpdateTotalAmountOnExisting,
                UpdateStartDateOnExisting = config.UpdateStartDateOnExisting,
                IsEnabled = config.IsEnabled,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
        }
    }
}
