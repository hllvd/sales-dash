using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    public class SqsQueueStats
    {
        public int ApproximateMessageCount { get; set; }
        public int ApproximateInFlightCount { get; set; }
        public string? OldestMessageAgeSeconds { get; set; }
        public bool IsConfigured { get; set; }
        public string QueueType { get; set; } = "results";
        public string? QueueName { get; set; }
    }

    public class SqsMessageDto
    {
        public string ReceiptHandle { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? RunId { get; set; }
        public string? UserId { get; set; }
        public string? Url { get; set; }
        public string? Matricula { get; set; }
        public string? Store { get; set; }
        public string? ScrapeType { get; set; }
        public List<string>? ScrapeDates { get; set; }
        public string? ScrapeDate { get; set; }
        public bool IsEncrypted { get; set; }
        public string? S3Key { get; set; }
        public string? S3Bucket { get; set; }
        public int? RowCount { get; set; }
        public string? CompletedAt { get; set; }
        public string? DurationFormatted { get; set; }
        public string? Status { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class ProcessMessageRequest
    {
        public string ReceiptHandle { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string? Queue { get; set; }
    }

    [ApiController]
    [Route("api/admin/sqs")]
    [Authorize(Roles = "SuperAdmin,superadmin")]
    public class AdminSqsController : ControllerBase
    {
        private readonly IAmazonSQS _sqs;
        private readonly IAmazonS3 _s3;
        private readonly IScrapeImportService _importService;
        private readonly IConfiguration _configuration;
        private readonly string? _jobsQueueUrl;
        private readonly string? _resultsQueueUrl;
        private readonly ILogger<AdminSqsController> _logger;

        public AdminSqsController(
            IAmazonSQS sqs,
            IAmazonS3 s3,
            IScrapeImportService importService,
            IConfiguration configuration,
            ILogger<AdminSqsController> logger)
        {
            _sqs = sqs;
            _s3 = s3;
            _importService = importService;
            _configuration = configuration;
            
            _jobsQueueUrl = configuration["AWS:SqsJobsQueueUrl"]
                         ?? configuration["AWS__SqsJobsQueueUrl"]
                         ?? configuration["SQS_JOBS_QUEUE_URL"]
                         ?? Environment.GetEnvironmentVariable("SQS_JOBS_QUEUE_URL")
                         ?? Environment.GetEnvironmentVariable("AWS__SqsJobsQueueUrl");

            _resultsQueueUrl = configuration["AWS:SqsResultsQueueUrl"]
                            ?? configuration["AWS__SqsResultsQueueUrl"]
                            ?? configuration["SQS_RESULTS_QUEUE_URL"]
                            ?? configuration["AWS:SqsQueueUrl"] 
                            ?? configuration["AWS__SqsQueueUrl"] 
                            ?? configuration["SQS_QUEUE_URL"] 
                            ?? Environment.GetEnvironmentVariable("SQS_RESULTS_QUEUE_URL")
                            ?? Environment.GetEnvironmentVariable("SQS_QUEUE_URL")
                            ?? Environment.GetEnvironmentVariable("AWS__SqsQueueUrl");

            _logger = logger;
        }

        private string? ResolveQueueUrl(string? queue)
        {
            if (string.Equals(queue, "jobs", StringComparison.OrdinalIgnoreCase))
            {
                return _jobsQueueUrl ?? _resultsQueueUrl;
            }
            return _resultsQueueUrl ?? _jobsQueueUrl;
        }

        private string GetQueueName(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "N/A";
            var parts = url.TrimEnd('/').Split('/');
            return parts.Length > 0 ? parts[^1] : url;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] string? queue = "results")
        {
            var targetQueue = queue?.ToLower() ?? "results";
            var queueUrl = ResolveQueueUrl(targetQueue);

            if (string.IsNullOrEmpty(queueUrl))
            {
                return Ok(new SqsQueueStats
                {
                    IsConfigured = false,
                    QueueType = targetQueue,
                    QueueName = null
                });
            }

            try
            {
                var attrs = await _sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    AttributeNames = new List<string>
                    {
                        "ApproximateNumberOfMessages",
                        "ApproximateNumberOfMessagesNotVisible",
                        "ApproximateNumberOfMessagesDelayed"
                    }
                });

                if (attrs == null)
                {
                    return Ok(new SqsQueueStats
                    {
                        IsConfigured = true,
                        QueueType = targetQueue,
                        QueueName = GetQueueName(queueUrl),
                        ApproximateMessageCount = 0,
                        ApproximateInFlightCount = 0,
                        OldestMessageAgeSeconds = null
                    });
                }

                return Ok(new SqsQueueStats
                {
                    IsConfigured = true,
                    QueueType = targetQueue,
                    QueueName = GetQueueName(queueUrl),
                    ApproximateMessageCount = attrs.ApproximateNumberOfMessages,
                    ApproximateInFlightCount = attrs.ApproximateNumberOfMessagesNotVisible,
                    OldestMessageAgeSeconds = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SQS queue stats for {Queue}", targetQueue);
                return StatusCode(500, new { message = $"Erro ao consultar fila SQS ({targetQueue}): {ex.Message}" });
            }
        }

        [HttpGet("messages")]
        public async Task<IActionResult> PeekMessages([FromQuery] int max = 10, [FromQuery] string? queue = "results")
        {
            var targetQueue = queue?.ToLower() ?? "results";
            var queueUrl = ResolveQueueUrl(targetQueue);

            if (string.IsNullOrEmpty(queueUrl))
                return Ok(new List<SqsMessageDto>());

            try
            {
                var effectiveMax = Math.Min(max, 10); // SQS max per receive is 10
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = effectiveMax,
                    VisibilityTimeout = 30, // 30s — short window to "peek" without long lock
                    WaitTimeSeconds = 0,    // no long-poll for peek
                    MessageSystemAttributeNames = new List<string> { "SentTimestamp" }
                };

                var response = await _sqs.ReceiveMessageAsync(receiveRequest);

                if (response?.Messages == null)
                    return Ok(new List<SqsMessageDto>());

                var messages = response.Messages.Select(m =>
                {
                    var dto = new SqsMessageDto
                    {
                        ReceiptHandle = m.ReceiptHandle,
                        MessageId = m.MessageId,
                        SentAt = m.Attributes.TryGetValue("SentTimestamp", out var ts)
                            ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(ts)).UtcDateTime
                            : DateTime.UtcNow
                    };

                    try
                    {
                        var body = JsonConvert.DeserializeObject<dynamic>(m.Body);
                        dto.JobId = body?.jobId?.ToString();
                        dto.RunId = body?.runId?.ToString();
                        dto.UserId = body?.userId?.ToString();
                        dto.Url = body?.url?.ToString();
                        dto.Matricula = body?.matricula?.ToString();
                        dto.Store = body?.store?.ToString() ?? body?.unit?.ToString();
                        dto.ScrapeType = body?.scrapeType?.ToString();
                        dto.Status = body?.status?.ToString();

                        if (body?.scrapeDates != null)
                        {
                            try
                            {
                                dto.ScrapeDates = JsonConvert.DeserializeObject<List<string>>(body.scrapeDates.ToString());
                            }
                            catch
                            {
                                dto.ScrapeDates = new List<string> { body.scrapeDates.ToString() };
                            }
                        }
                        dto.ScrapeDate = body?.scrapeDate?.ToString();

                        dto.IsEncrypted = body?.encryptedPassword != null || body?.encryptedUsername != null;

                        dto.S3Key = body?.s3Key?.ToString();
                        dto.S3Bucket = body?.s3Bucket?.ToString();
                        dto.RowCount = body?.rowCount != null ? (int?)body.rowCount : null;
                        dto.CompletedAt = body?.completedAt?.ToString();
                        dto.DurationFormatted = body?.durationFormatted?.ToString();
                    }
                    catch { /* body parse error — return raw info only */ }

                    return dto;
                }).ToList();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error peeking SQS messages from {Queue}", targetQueue);
                return StatusCode(500, new { message = $"Erro ao ler mensagens da fila ({targetQueue}): {ex.Message}" });
            }
        }

        [HttpPost("messages/process")]
        public async Task<IActionResult> ProcessMessage([FromBody] ProcessMessageRequest request)
        {
            var queueUrl = ResolveQueueUrl(request.Queue ?? "results");
            if (string.IsNullOrEmpty(queueUrl))
                return BadRequest(new { message = "SQS não configurado." });

            if (string.IsNullOrEmpty(request.ReceiptHandle) || string.IsNullOrEmpty(request.MessageBody))
                return BadRequest(new { message = "receiptHandle e messageBody são obrigatórios." });

            try
            {
                var body = JsonConvert.DeserializeObject<dynamic>(request.MessageBody)
                    ?? throw new InvalidOperationException("Message body is empty or invalid JSON.");

                string s3Key = body.s3Key?.ToString()
                    ?? throw new InvalidOperationException("Mensagem não contém s3Key.");
                string s3Bucket = body.s3Bucket?.ToString()
                    ?? _configuration["AWS:ScrapeResultsBucket"]
                    ?? "hdev-sales-dash";
                string? rawUserId = body.userId?.ToString();
                Guid? userId = null;
                if (!string.IsNullOrEmpty(rawUserId) && Guid.TryParse(rawUserId, out Guid parsedUid))
                {
                    userId = parsedUid;
                }

                var importResult = await _importService.AutoImportFromS3Async(s3Bucket, s3Key, userId);

                if (importResult.Errors.Any())
                {
                    _logger.LogWarning("SQS message processed with errors: {Errors}", string.Join(" | ", importResult.Errors));
                    // Do NOT delete message so it can be retried
                    return StatusCode(422, new
                    {
                        success = false,
                        errors = importResult.Errors,
                        message = "Importação falhou. Mensagem NÃO removida da fila."
                    });
                }

                // Delete from SQS only on success
                await _sqs.DeleteMessageAsync(queueUrl, request.ReceiptHandle);

                var importedCount = importResult.ProcessedRows;
                return Ok(new
                {
                    success = true,
                    importedCount = importedCount,
                    message = $"Importação concluída. {importedCount} contrato(s) processado(s). Mensagem removida da fila."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SQS message");
                return StatusCode(500, new { message = $"Erro ao processar mensagem: {ex.Message}" });
            }
        }

        [HttpDelete("messages")]
        public async Task<IActionResult> DiscardMessage([FromBody] ProcessMessageRequest request, [FromQuery] string? queue = null)
        {
            var targetQueue = queue ?? request.Queue ?? "results";
            var queueUrl = ResolveQueueUrl(targetQueue);

            if (string.IsNullOrEmpty(queueUrl))
                return BadRequest(new { message = "SQS não configurado." });

            if (string.IsNullOrEmpty(request.ReceiptHandle))
                return BadRequest(new { message = "receiptHandle é obrigatório." });

            try
            {
                await _sqs.DeleteMessageAsync(queueUrl, request.ReceiptHandle);
                return Ok(new { success = true, message = $"Mensagem descartada da fila ({targetQueue})." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discarding SQS message from {Queue}", targetQueue);
                return StatusCode(500, new { message = $"Erro ao descartar mensagem: {ex.Message}" });
            }
        }
    }
}
