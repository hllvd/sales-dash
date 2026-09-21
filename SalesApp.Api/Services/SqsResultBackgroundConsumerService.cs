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

            _queueUrl = configuration["AWS:SqsResultsQueueUrl"]
                     ?? configuration["AWS__SqsResultsQueueUrl"]
                     ?? configuration["SQS_RESULTS_QUEUE_URL"]
                     ?? configuration["AWS:SqsQueueUrl"]
                     ?? configuration["AWS__SqsQueueUrl"]
                     ?? configuration["SQS_QUEUE_URL"]
                     ?? Environment.GetEnvironmentVariable("SQS_RESULTS_QUEUE_URL")
                     ?? Environment.GetEnvironmentVariable("SQS_QUEUE_URL")
                     ?? Environment.GetEnvironmentVariable("AWS__SqsQueueUrl");

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
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20,
                        VisibilityTimeout = 60,
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

                string? s3Key = body.s3Key?.ToString();
                if (string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogDebug("SqsResultBackgroundConsumerService: Message {MessageId} does not contain s3Key (ignoring non-result message).", message.MessageId);
                    return;
                }

                string s3Bucket = body.s3Bucket?.ToString()
                    ?? _configuration["AWS:ScrapeResultsBucket"]
                    ?? "hdev-sales-dash";

                string? rawUserId = body.userId?.ToString();
                string? jobId = body.jobId?.ToString();
                string? runId = body.runId?.ToString();
                string? matricula = body.matricula?.ToString();
                string? store = body.store?.ToString() ?? body.unit?.ToString();

                Guid? userId = null;
                if (!string.IsNullOrEmpty(rawUserId) && Guid.TryParse(rawUserId, out var parsedUid))
                {
                    userId = parsedUid;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var importService = scope.ServiceProvider.GetRequiredService<IScrapeImportService>();
                var logService = scope.ServiceProvider.GetRequiredService<IScrapeDynamoLogService>();

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

                _logger.LogInformation(
                    "SqsResultBackgroundConsumerService: Processing auto-import from s3://{Bucket}/{Key} (JobId: {JobId}, RunId: {RunId}, User: {User})...",
                    s3Bucket, s3Key, jobId ?? "N/A", runId ?? "N/A", userId?.ToString() ?? "N/A");

                var importResult = await importService.AutoImportFromS3Async(s3Bucket, s3Key, userId);

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
                                new { error = $"Auto-import failed: {errorsJoined}" });
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
                                autoImported = true
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
    }
}
