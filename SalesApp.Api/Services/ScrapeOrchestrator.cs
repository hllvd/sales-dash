using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using Microsoft.AspNetCore.DataProtection;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SalesApp.Services
{
    public interface IScrapeOrchestrator
    {
        Task<string> TriggerScrapeAsync(int configId, bool isManual = true, string? runId = null, string? userEmail = null, string? scrapeDate = null, string? scrapeType = null, string? outputMode = null);
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
        private readonly IConfiguration _configuration;
        private readonly IAmazonSQS? _sqs;
        private readonly string? _jobsQueueUrl;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public ScrapeOrchestrator(
            AppDbContext context,
            PbiScraperClient scraperClient,
            IScrapeDynamoLogService logService,
            IScrapeImportService importService,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            IAmazonSQS? sqs = null)
        {
            _context = context;
            _scraperClient = scraperClient;
            _logService = logService;
            _importService = importService;
            _protector = dataProtectionProvider.CreateProtector("ScrapeConfig.PowerBiPassword");
            _configuration = configuration;
            _outputDir = configuration["PbiScraper:OutputDir"] ?? "./outputs";
            _sqs = sqs;

            _jobsQueueUrl = configuration["AWS:SqsJobsQueueUrl"]
                         ?? configuration["AWS__SqsJobsQueueUrl"]
                         ?? configuration["SQS_JOBS_QUEUE_URL"]
                         ?? configuration["AWS:SqsQueueUrl"]
                         ?? configuration["AWS__SqsQueueUrl"]
                         ?? configuration["SQS_QUEUE_URL"]
                         ?? Environment.GetEnvironmentVariable("SQS_JOBS_QUEUE_URL")
                         ?? Environment.GetEnvironmentVariable("SQS_QUEUE_URL")
                         ?? Environment.GetEnvironmentVariable("AWS__SqsJobsQueueUrl")
                         ?? Environment.GetEnvironmentVariable("AWS__SqsQueueUrl");
        }

        public async Task<string> TriggerScrapeAsync(int configId, bool isManual = true, string? runId = null, string? userEmail = null, string? scrapeDate = null, string? scrapeType = null, string? outputMode = null)
        {
            var config = await _context.ScrapeConfigs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == configId);

            if (config == null) throw new ArgumentException($"Config {configId} not found");

            var jobId = Guid.NewGuid().ToString();
            var effectiveRunId = string.IsNullOrEmpty(runId) ? Guid.NewGuid().ToString() : runId;
            var effectiveUserEmail = userEmail;
            var effectiveScrapeType = !string.IsNullOrWhiteSpace(scrapeType) ? scrapeType : (config.ScrapeType ?? "geral");
            var effectiveOutputMode = !string.IsNullOrWhiteSpace(outputMode) ? outputMode : (config.OutputMode ?? "direct");

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
                store: config.Store ?? string.Empty,
                matricula: config.Matricula,
                runId: effectiveRunId,
                userEmail: effectiveUserEmail,
                additionalData: new { ScrapeDate = scrapeDate, ScrapeType = effectiveScrapeType, OutputMode = effectiveOutputMode }
            );

            try
            {
                if (string.Equals(effectiveOutputMode, "sqs", StringComparison.OrdinalIgnoreCase))
                {
                    // Modo Remoto (AWS Fargate Spot via SQS): enfileira na fila de entrada e aciona a Lambda
                    await EnqueueRemoteFargateJobAsync(config, jobId, effectiveRunId, scrapeDate, effectiveScrapeType);
                    await TryInvokeScraperLauncherLambdaAsync(jobId, effectiveRunId, config.Matricula);
                }
                else
                {
                    // Modo Local: container local pbi-scraper na VPS
                    await _scraperClient.EnqueueJobAsync(
                        jobId: jobId,
                        runId: effectiveRunId,
                        userId: config.UserId?.ToString() ?? string.Empty,
                        store: config.Store ?? string.Empty,
                        matricula: config.Matricula,
                        avaproUsername: config.Matricula,
                        avaproPassword: config.PowerBiPassword,
                        scrapeDate: scrapeDate,
                        scrapeType: effectiveScrapeType,
                        outputMode: effectiveOutputMode
                    );
                }
                
                // Update status to Running
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId?.ToString() ?? string.Empty,
                    status: "Running",
                    store: config.Store ?? string.Empty,
                    matricula: config.Matricula,
                    runId: effectiveRunId,
                    userEmail: effectiveUserEmail,
                    additionalData: new { ScrapeDate = scrapeDate, ScrapeType = effectiveScrapeType, OutputMode = effectiveOutputMode }
                );
            }
            catch (Exception ex)
            {
                await _logService.WriteJobStatusAsync(
                    jobId: jobId,
                    userId: config.UserId?.ToString() ?? string.Empty,
                    status: "Failed",
                    store: config.Store ?? string.Empty,
                    matricula: config.Matricula,
                    runId: effectiveRunId,
                    userEmail: effectiveUserEmail,
                    additionalData: new { ErrorMessage = ex.Message, ScrapeDate = scrapeDate, ScrapeType = effectiveScrapeType, OutputMode = effectiveOutputMode }
                );
                throw;
            }

            return jobId;
        }


        public async Task HandleCallbackAsync(ScrapeResult result)
        {
            var effectiveStore = result.DetectedStore ?? result.Store ?? "Unknown";

            // If store in DB is currently null/empty, auto-populate with detected store
            if (!string.IsNullOrEmpty(effectiveStore) && effectiveStore != "Unknown" && !string.IsNullOrEmpty(result.Matricula))
            {
                var configToUpdate = await _context.ScrapeConfigs.FirstOrDefaultAsync(c => c.Matricula == result.Matricula);
                if (configToUpdate != null && string.IsNullOrEmpty(configToUpdate.Store))
                {
                    configToUpdate.Store = effectiveStore;
                    configToUpdate.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            // If authentication failed with wrong-password, trip the circuit breaker in DB
            if (string.Equals(result.AuthStatus, "wrong-password", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(result.Matricula))
            {
                var configToLock = await _context.ScrapeConfigs.FirstOrDefaultAsync(c => c.Matricula == result.Matricula);
                if (configToLock != null && configToLock.CredentialStatus != "wrong-password")
                {
                    configToLock.CredentialStatus = "wrong-password";
                    configToLock.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            // 1. Update DynamoDB status
            await _logService.WriteJobStatusAsync(
                jobId: result.JobId,
                userId: result.UserId,
                status: result.Status,
                store: effectiveStore,
                matricula: result.Matricula ?? "Unknown",
                runId: result.RunId,
                additionalData: new
                {
                    RowCount = result.RowCount,
                    FileRelativePath = result.FileRelativePath,
                    ErrorMessage = result.Error,
                    CompletedAt = !string.IsNullOrEmpty(result.CompletedAt) ? result.CompletedAt : DateTime.UtcNow.ToString("O"),
                    StartedAt = result.StartedAt,
                    DurationSeconds = result.DurationSeconds,
                    DurationFormatted = result.DurationFormatted,
                    AuthStatus = result.AuthStatus,
                    AuthMessage = result.AuthMessage,
                    PowerBiLoaded = result.PowerBiLoaded,
                    AuthSteps = result.AuthSteps != null ? Newtonsoft.Json.JsonConvert.SerializeObject(result.AuthSteps) : null,
                    RetryCount = result.RetryCount,
                    ScrapeDate = result.ScrapeDate
                }
            );

            // 2. Trigger Auto-Import if success — only in "direct" mode
            // In "sqs" mode, the sqs-worker will call POST /api/scrape/import-from-s3 after consuming the queue message
            var isSqsMode = string.Equals(result.OutputMode, "sqs", StringComparison.OrdinalIgnoreCase);

            if (result.Status == "Succeeded" && !isSqsMode && !string.IsNullOrEmpty(result.FileRelativePath))
            {
                var filePath = Path.Combine(_outputDir, result.FileRelativePath);
                try
                {
                    Guid? userId = Guid.TryParse(result.UserId, out var uid) ? uid : (Guid?)null;
                    var importResult = await _importService.AutoImportAsync(filePath, userId);
                    
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
            else if (result.Status == "Succeeded" && isSqsMode && !string.IsNullOrEmpty(result.S3Key))
            {
                // Log that the file is waiting in SQS/S3 for the worker to consume
                await _logService.WriteJobStatusAsync(
                    jobId: result.JobId,
                    userId: result.UserId,
                    status: "AwaitingImport",
                    store: result.Store ?? "Unknown",
                    matricula: result.Matricula ?? "Unknown",
                    runId: result.RunId,
                    additionalData: new { S3Key = result.S3Key, S3Bucket = result.S3Bucket, Message = "CSV enfileirado no SQS. Aguardando worker local importar." }
                );
            }
        }

        private async Task TryInvokeScraperLauncherLambdaAsync(string jobId, string runId, string matricula)
        {
            var lambdaUrl = _configuration["AWS:ScraperLauncherLambdaUrl"]
                         ?? _configuration["AWS__ScraperLauncherLambdaUrl"]
                         ?? _configuration["SCRAPER_LAUNCHER_LAMBDA_URL"]
                         ?? Environment.GetEnvironmentVariable("SCRAPER_LAUNCHER_LAMBDA_URL")
                         ?? Environment.GetEnvironmentVariable("AWS__ScraperLauncherLambdaUrl");

            if (string.IsNullOrWhiteSpace(lambdaUrl))
            {
                Console.WriteLine("[ScrapeOrchestrator] Aviso: URL da Lambda launcher não configurada (SCRAPER_LAUNCHER_LAMBDA_URL). O Fargate Spot não será disparado automaticamente.");
                return;
            }

            try
            {
                Console.WriteLine($"[ScrapeOrchestrator] Disparando Lambda launcher em {lambdaUrl} para job {jobId} (runId: {runId})...");
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    jobId,
                    runId,
                    matricula,
                    workerCount = 1,
                    useSpot = true,
                    timestamp = DateTime.UtcNow.ToString("o")
                });

                using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(lambdaUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ScrapeOrchestrator] Aviso: Lambda de launcher retornou status {response.StatusCode} para job {jobId}. Resposta: {responseBody}");
                }
                else
                {
                    Console.WriteLine($"[ScrapeOrchestrator] Sucesso: Lambda de launcher executada com status {response.StatusCode} para job {jobId}. Resposta: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScrapeOrchestrator] Erro: Falha ao acionar Lambda de launcher para job {jobId}: {ex.Message}");
            }
        }

        private async Task EnqueueRemoteFargateJobAsync(ScrapeConfig config, string jobId, string runId, string? scrapeDate, string scrapeType)
        {
            if (_sqs == null || string.IsNullOrWhiteSpace(_jobsQueueUrl))
            {
                throw new InvalidOperationException("Fila SQS de jobs não configurada ou serviço SQS indisponível para execução remota no Fargate.");
            }

            string plainPassword = config.PowerBiPassword ?? string.Empty;
            if (string.IsNullOrEmpty(plainPassword))
            {
                try
                {
                    plainPassword = _protector.Unprotect(config.PowerBiPassword);
                }
                catch
                {
                    plainPassword = config.PowerBiPassword ?? string.Empty;
                }
            }

            var encryptionKey = _configuration["SCRAPER_ENCRYPTION_KEY"]
                             ?? Environment.GetEnvironmentVariable("SCRAPER_ENCRYPTION_KEY");

            var payload = new Dictionary<string, object?>
            {
                ["jobId"] = jobId,
                ["runId"] = runId,
                ["userId"] = config.UserId?.ToString() ?? string.Empty,
                ["matricula"] = config.Matricula,
                ["store"] = config.Store ?? string.Empty,
                ["scrapeDate"] = scrapeDate,
                ["scrapeType"] = scrapeType,
                ["outputMode"] = "sqs",
                ["callbackUrl"] = "" // O Fargate notifica via fila SQS_RESULTS_QUEUE_URL
            };

            if (!string.IsNullOrWhiteSpace(encryptionKey) && encryptionKey.Trim().Length == 64)
            {
                var (cipherTextB64, ivB64, authTagB64) = ScraperCredentialEncryption.Encrypt(plainPassword, encryptionKey);
                payload["encryptedPassword"] = cipherTextB64;
                payload["passwordIv"] = ivB64;
                payload["passwordAuthTag"] = authTagB64;
            }
            else
            {
                payload["avaproPassword"] = plainPassword;
                payload["password"] = plainPassword;
            }

            var messageBody = System.Text.Json.JsonSerializer.Serialize(payload);
            await _sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _jobsQueueUrl,
                MessageBody = messageBody
            });
        }
    }
}

