using System.Collections.Concurrent;
using System.Globalization;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class SqsResultBackgroundConsumerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAmazonSQS _sqs;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqsResultBackgroundConsumerService> _logger;
        private readonly string? _queueUrl;
        private readonly bool _isEnabled;
        private static readonly SemaphoreSlim _importLock = new(1, 1);
        private static readonly ConcurrentDictionary<string, DateTime> _processedJobIds = new();
        private const int MessageVisibilityTimeoutSeconds = 600;
        private const int VisibilityHeartbeatIntervalSeconds = 90;

        public SqsResultBackgroundConsumerService(
            IServiceScopeFactory scopeFactory,
            IAmazonSQS sqs,
            IConfiguration configuration,
            ILogger<SqsResultBackgroundConsumerService> logger)
        {
            _scopeFactory = scopeFactory;
            _sqs = sqs;
            _configuration = configuration;
            _logger = logger;

            _queueUrl = FirstNonWhiteSpace(
                configuration["AWS:SqsResultsQueueUrl"],
                configuration["AWS__SqsResultsQueueUrl"],
                configuration["SQS_RESULTS_QUEUE_URL"],
                Environment.GetEnvironmentVariable("SQS_RESULTS_QUEUE_URL"),
                configuration["AWS:SqsQueueUrl"],
                configuration["AWS__SqsQueueUrl"],
                configuration["SQS_QUEUE_URL"],
                Environment.GetEnvironmentVariable("SQS_QUEUE_URL"),
                Environment.GetEnvironmentVariable("AWS__SqsQueueUrl")
            );

            if (string.IsNullOrWhiteSpace(_queueUrl))
            {
                var isDev = string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                _queueUrl = isDev 
                    ? "https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-dash"
                    : "https://sqs.us-east-1.amazonaws.com/797163826126/hdev-sales-dash";
            }

            var enabledStr = configuration["AWS:EnableSqsBackgroundConsumer"]
                          ?? configuration["AWS__EnableSqsBackgroundConsumer"]
                          ?? Environment.GetEnvironmentVariable("AWS__EnableSqsBackgroundConsumer")
                          ?? Environment.GetEnvironmentVariable("AWS_ENABLE_SQS_BACKGROUND_CONSUMER");

            _isEnabled = string.IsNullOrEmpty(enabledStr) || !bool.TryParse(enabledStr, out var val) || val;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_isEnabled)
            {
                _logger.LogInformation("SqsResultBackgroundConsumerService is disabled via configuration.");
                return;
            }

            if (string.IsNullOrEmpty(_queueUrl))
            {
                _logger.LogWarning("SqsResultBackgroundConsumerService: SQS Results Queue URL is not configured. Background consumer will be idle.");
                return;
            }

            _logger.LogInformation("SqsResultBackgroundConsumerService started. Listening on SQS queue: {QueueUrl}", _queueUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveRequest = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 1,
                        WaitTimeSeconds = 20,
                        VisibilityTimeout = MessageVisibilityTimeoutSeconds,
                        MessageSystemAttributeNames = new List<string> { "All" },
                        MessageAttributeNames = new List<string> { "All" }
                    };

                    var response = await _sqs.ReceiveMessageAsync(receiveRequest, stoppingToken);

                    if (response.Messages == null || response.Messages.Count == 0)
                    {
                        continue;
                    }

                    _logger.LogInformation("SqsResultBackgroundConsumerService received {Count} message(s) from SQS.", response.Messages.Count);

                    foreach (var message in response.Messages)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await ProcessSingleMessageAsync(message, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling or consuming SQS messages in SqsResultBackgroundConsumerService. Retrying in 5s...");
                    try
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("SqsResultBackgroundConsumerService stopping.");
        }

        private async Task ProcessSingleMessageAsync(Message message, CancellationToken stoppingToken)
        {
            try
            {
                var body = JsonConvert.DeserializeObject<dynamic>(message.Body);
                if (body == null)
                {
                    _logger.LogWarning("SqsResultBackgroundConsumerService: Received empty message body ({MessageId}).", message.MessageId);
                    return;
                }

                string? rawStatus = body.status?.ToString();
                string? s3Key = body.s3Key?.ToString();

                // If message has neither status nor s3Key, it is not a result message (e.g. raw job message)
                if (string.IsNullOrEmpty(rawStatus) && string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogWarning("SqsResultBackgroundConsumerService: Message {MessageId} contains neither status nor s3Key. Ignoring non-result message.", message.MessageId);
                    return;
                }

                string status = !string.IsNullOrEmpty(rawStatus) ? rawStatus : "Succeeded";
                string? error = body.error?.ToString();

                string s3Bucket = body.s3Bucket?.ToString()
                    ?? _configuration["AWS:ScrapeResultsBucket"]
                    ?? "hdev-sales-dash";

                string? rawUserId = body.userId?.ToString();
                string? jobId = body.jobId?.ToString();
                string? runId = body.runId?.ToString();
                string? matricula = body.matricula?.ToString();
                string? store = body.store?.ToString() ?? body.unit?.ToString();
                string? scrapeDate = body.scrapeDate?.ToString();
                string? durationFormatted = body.durationFormatted?.ToString();
                int? durationSeconds = null;
                string? durSecsRaw = body.durationSeconds?.ToString();
                if (!string.IsNullOrEmpty(durSecsRaw) && int.TryParse(durSecsRaw, out int ds))
                {
                    durationSeconds = ds;
                }
                string? authStatus = body.authStatus?.ToString() ?? (status == "Failed" ? "wrong-password" : "success");
                string? authMessage = body.authMessage?.ToString() ?? (status == "Failed" ? (error ?? "Usuário ou senha inválida no AVA PRO.") : "Autenticação bem-sucedida");
                bool powerBiLoaded = true;
                string? pblRaw = body.powerbiLoaded?.ToString() ?? body.powerBiLoaded?.ToString();
                if (!string.IsNullOrEmpty(pblRaw) && bool.TryParse(pblRaw, out bool pbl))
                {
                    powerBiLoaded = pbl;
                }
                else if (status == "Failed" || string.Equals(authStatus, "wrong-password", StringComparison.OrdinalIgnoreCase))
                {
                    powerBiLoaded = false;
                }
                string? authSteps = body.authSteps != null ? body.authSteps.ToString() : null;
                string? completedAt = body.completedAt?.ToString() ?? body.timestamp?.ToString() ?? DateTime.UtcNow.ToString("O");

                Guid? userId = null;
                if (!string.IsNullOrEmpty(rawUserId) && Guid.TryParse(rawUserId, out var parsedUid))
                {
                    userId = parsedUid;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var importService = scope.ServiceProvider.GetRequiredService<IScrapeImportService>();
                var logService = scope.ServiceProvider.GetRequiredService<IScrapeDynamoLogService>();

                // Handle failed scrape jobs (such as authentication or wrong password failures)
                if (status == "Failed")
                {
                    _logger.LogWarning(
                        "SqsResultBackgroundConsumerService: Received failed job notification (JobId: {JobId}, RunId: {RunId}, Matricula: {Matricula}, AuthStatus: {AuthStatus}, Error: {Error}).",
                        jobId ?? "N/A", runId ?? "N/A", matricula ?? "N/A", authStatus, error ?? authMessage);

                    if (!string.IsNullOrWhiteSpace(matricula))
                    {
                        var config = await dbContext.ScrapeConfigs
                            .Include(c => c.User)
                            .FirstOrDefaultAsync(c => c.Matricula == matricula.Trim(), stoppingToken);

                        if (config != null)
                        {
                            if (!userId.HasValue && config.User?.Id != null)
                            {
                                userId = config.User.Id;
                            }

                            if (string.Equals(authStatus, "wrong-password", StringComparison.OrdinalIgnoreCase) && config.CredentialStatus != "wrong-password")
                            {
                                config.CredentialStatus = "wrong-password";
                                config.UpdatedAt = DateTime.UtcNow;
                                await dbContext.SaveChangesAsync(stoppingToken);
                                _logger.LogWarning("SqsResultBackgroundConsumerService: Trava de seguranca ('wrong-password') ativada no ScrapeConfig para matricula {Matricula}.", matricula);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(jobId))
                    {
                        var effectiveError = !string.IsNullOrWhiteSpace(authMessage) ? authMessage : (!string.IsNullOrWhiteSpace(error) ? error : "Falha na extração");
                        await logService.WriteJobStatusAsync(
                            jobId: jobId,
                            userId: userId?.ToString() ?? rawUserId ?? string.Empty,
                            status: "Failed",
                            store: store ?? string.Empty,
                            matricula: matricula ?? string.Empty,
                            runId: runId,
                            userEmail: null,
                            additionalData: new
                            {
                                ErrorMessage = effectiveError,
                                ScrapeDate = scrapeDate,
                                DurationFormatted = durationFormatted,
                                DurationSeconds = durationSeconds,
                                AuthStatus = authStatus,
                                AuthMessage = effectiveError,
                                PowerBiLoaded = powerBiLoaded,
                                AuthSteps = authSteps,
                                CompletedAt = completedAt
                            }
                        );
                    }

                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    return;
                }

                // Check ScrapeConfig if auto-import is enabled for this account
                if (!string.IsNullOrWhiteSpace(matricula))
                {
                    var config = await dbContext.ScrapeConfigs
                        .Include(c => c.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Matricula == matricula.Trim(), stoppingToken);

                    if (config != null && !config.AutoImportSqs)
                    {
                        _logger.LogInformation(
                            "SqsResultBackgroundConsumerService: AutoImportSqs is disabled for matricula {Matricula}. Leaving message {MessageId} in queue for manual processing.",
                            matricula, message.MessageId);
                        return;
                    }

                    if (!userId.HasValue && config?.User?.Id != null)
                    {
                        userId = config.User.Id;
                    }
                }

                if (string.IsNullOrWhiteSpace(s3Key))
                {
                    _logger.LogWarning(
                        "SqsResultBackgroundConsumerService: Message {MessageId} (JobId: {JobId}) has status '{Status}' but missing s3Key. Discarding message.",
                        message.MessageId, jobId ?? "N/A", status);
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(jobId) && IsRecentlyProcessedJob(jobId))
                {
                    _logger.LogInformation(
                        "SqsResultBackgroundConsumerService: Job {JobId} was already imported recently. Discarding duplicate SQS message {MessageId}.",
                        jobId, message.MessageId);
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    return;
                }

                await _importLock.WaitAsync(stoppingToken);
                ImportResult importResult;
                try
                {
                    // Re-check inside lock in case a concurrent delivery completed while waiting
                    if (!string.IsNullOrWhiteSpace(jobId) && IsRecentlyProcessedJob(jobId))
                    {
                        _logger.LogInformation(
                            "SqsResultBackgroundConsumerService: Job {JobId} completed while waiting for import lock. Discarding duplicate SQS message {MessageId}.",
                            jobId, message.MessageId);
                        await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                        return;
                    }

                    _logger.LogInformation(
                        "SqsResultBackgroundConsumerService: Processing auto-import from s3://{Bucket}/{Key} (JobId: {JobId}, RunId: {RunId}, User: {User})...",
                        s3Bucket, s3Key, jobId ?? "N/A", runId ?? "N/A", userId?.ToString() ?? "N/A");

                    importResult = await RunWithVisibilityHeartbeatAsync(
                        message.ReceiptHandle,
                        () => importService.AutoImportFromS3Async(s3Bucket, s3Key, userId),
                        stoppingToken);
                }
                finally
                {
                    _importLock.Release();
                }

                if (importResult.Errors.Any())
                {
                    var errorsJoined = string.Join(" | ", importResult.Errors);
                    _logger.LogWarning(
                        "SqsResultBackgroundConsumerService: Auto-import reported errors for message {MessageId}: {Errors}",
                        message.MessageId, errorsJoined);

                    if (!string.IsNullOrEmpty(jobId) && userId.HasValue)
                    {
                        try
                        {
                            await logService.WriteJobStatusAsync(
                                jobId,
                                userId.Value.ToString(),
                                "Failed",
                                store ?? string.Empty,
                                matricula ?? string.Empty,
                                runId,
                                null,
                                new { 
                                    error = $"Auto-import failed: {errorsJoined}",
                                    ScrapeDate = scrapeDate
                                });
                        }
                        catch (Exception logEx)
                        {
                            _logger.LogWarning(logEx, "Failed to write error log to DynamoDB for JobId: {JobId}", jobId);
                        }
                    }

                    // If the S3 key definitely does not exist (orphan or expired file), discard the dead message from SQS
                    bool isOrphanMissingKey = importResult.Errors.Any(e =>
                        e.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase) ||
                        e.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                        e.Contains("NotFound", StringComparison.OrdinalIgnoreCase));

                    if (isOrphanMissingKey)
                    {
                        _logger.LogWarning(
                            "SqsResultBackgroundConsumerService: S3 key '{Key}' was not found in bucket '{Bucket}'. Discarding orphan SQS message {MessageId}.",
                            s3Key, s3Bucket, message.MessageId);
                        await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    }

                    return;
                }

                if (!string.IsNullOrWhiteSpace(jobId))
                {
                    MarkJobProcessed(jobId);
                }

                // Delete message from SQS upon successful ingestion
                await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);

                _logger.LogInformation(
                    "SqsResultBackgroundConsumerService: Successfully imported {Count} contract(s) from s3://{Bucket}/{Key}. SQS message {MessageId} deleted.",
                    importResult.ProcessedRows, s3Bucket, s3Key, message.MessageId);

                if (!string.IsNullOrEmpty(jobId) && userId.HasValue)
                {
                    try
                    {
                        await logService.WriteJobStatusAsync(
                            jobId,
                            userId.Value.ToString(),
                            "Succeeded",
                            store ?? string.Empty,
                            matricula ?? string.Empty,
                            runId,
                            null,
                            new
                            {
                                rowCount = importResult.ProcessedRows,
                                s3Bucket,
                                s3Key,
                                autoImported = true,
                                ScrapeDate = scrapeDate,
                                DurationFormatted = durationFormatted,
                                DurationSeconds = durationSeconds,
                                AuthStatus = authStatus,
                                AuthMessage = authMessage,
                                PowerBiLoaded = powerBiLoaded,
                                AuthSteps = authSteps,
                                CompletedAt = completedAt
                            });
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Failed to update success log in DynamoDB for JobId: {JobId}", jobId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SqsResultBackgroundConsumerService: Error processing message {MessageId}", message.MessageId);
            }
        }

        private async Task<T> RunWithVisibilityHeartbeatAsync<T>(string receiptHandle, Func<Task<T>> operation, CancellationToken stoppingToken)
        {
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var heartbeatTask = Task.Run(async () =>
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(VisibilityHeartbeatIntervalSeconds), heartbeatCts.Token);
                        if (string.IsNullOrEmpty(_queueUrl) || heartbeatCts.Token.IsCancellationRequested) break;

                        await _sqs.ChangeMessageVisibilityAsync(
                            _queueUrl,
                            receiptHandle,
                            MessageVisibilityTimeoutSeconds,
                            heartbeatCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SqsResultBackgroundConsumerService: Failed to extend SQS message visibility.");
                    }
                }
            }, CancellationToken.None);

            try
            {
                return await operation();
            }
            finally
            {
                heartbeatCts.Cancel();
                try { await heartbeatTask; } catch { /* ignored */ }
            }
        }

        private static bool IsRecentlyProcessedJob(string jobId)
        {
            var cutoff = DateTime.UtcNow.AddHours(-6);
            foreach (var kvp in _processedJobIds)
            {
                if (kvp.Value < cutoff)
                {
                    _processedJobIds.TryRemove(kvp.Key, out _);
                }
            }

            return _processedJobIds.TryGetValue(jobId, out var processedAt) && processedAt >= cutoff;
        }

        private static void MarkJobProcessed(string jobId)
        {
            _processedJobIds[jobId] = DateTime.UtcNow;
        }

        private static string? FirstNonWhiteSpace(params string?[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c)) return c.Trim();
            }
            return null;
        }
    }
}
