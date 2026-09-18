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
    }

    public class SqsMessageDto
    {
        public string ReceiptHandle { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? S3Key { get; set; }
        public string? S3Bucket { get; set; }
        public int? RowCount { get; set; }
        public string? Matricula { get; set; }
        public string? Store { get; set; }
        public string? ScrapeDate { get; set; }
        public string? CompletedAt { get; set; }
        public string? DurationFormatted { get; set; }
        public string? UserId { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class ProcessMessageRequest
    {
        public string ReceiptHandle { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/admin/sqs")]
    [Authorize(Roles = "superadmin")]
    public class AdminSqsController : ControllerBase
    {
        private readonly IAmazonSQS _sqs;
        private readonly IAmazonS3 _s3;
        private readonly IScrapeImportService _importService;
        private readonly IConfiguration _configuration;
        private readonly string? _queueUrl;
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
            _queueUrl = configuration["AWS:SqsQueueUrl"] 
                     ?? configuration["AWS__SqsQueueUrl"] 
                     ?? configuration["SQS_QUEUE_URL"] 
                     ?? Environment.GetEnvironmentVariable("SQS_QUEUE_URL")
                     ?? Environment.GetEnvironmentVariable("AWS__SqsQueueUrl");
            _logger = logger;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            if (string.IsNullOrEmpty(_queueUrl))
                return Ok(new SqsQueueStats { IsConfigured = false });

            try
            {
                var attrs = await _sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = _queueUrl,
                    AttributeNames = new List<string>
                    {
                        "ApproximateNumberOfMessages",
                        "ApproximateNumberOfMessagesNotVisible",
                        "ApproximateAgeOfOldestMessage"
                    }
                });

                return Ok(new SqsQueueStats
                {
                    IsConfigured = true,
                    ApproximateMessageCount = int.TryParse(attrs.ApproximateNumberOfMessages.ToString(), out var count) ? count : 0,
                    ApproximateInFlightCount = int.TryParse(attrs.ApproximateNumberOfMessagesNotVisible.ToString(), out var inflight) ? inflight : 0,
                    OldestMessageAgeSeconds = attrs.Attributes.TryGetValue("ApproximateAgeOfOldestMessage", out var age) ? age : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SQS queue stats");
                return StatusCode(500, new { message = $"Erro ao consultar fila SQS: {ex.Message}" });
            }
        }

        [HttpGet("messages")]
        public async Task<IActionResult> PeekMessages([FromQuery] int max = 10)
        {
            if (string.IsNullOrEmpty(_queueUrl))
                return Ok(new List<SqsMessageDto>());

            try
            {
                var effectiveMax = Math.Min(max, 10); // SQS max per receive is 10
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = effectiveMax,
                    VisibilityTimeout = 30, // 30s — short window to "peek" without long lock
                    WaitTimeSeconds = 0,    // no long-poll for peek
                    MessageSystemAttributeNames = new List<string> { "SentTimestamp" }
                };

                var response = await _sqs.ReceiveMessageAsync(receiveRequest);

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
                        dto.S3Key = body?.s3Key?.ToString();
                        dto.S3Bucket = body?.s3Bucket?.ToString();
                        dto.RowCount = body?.rowCount != null ? (int?)body.rowCount : null;
                        dto.Matricula = body?.matricula?.ToString();
                        dto.Store = body?.store?.ToString();
                        dto.ScrapeDate = body?.scrapeDate?.ToString();
                        dto.CompletedAt = body?.completedAt?.ToString();
                        dto.DurationFormatted = body?.durationFormatted?.ToString();
                        dto.UserId = body?.userId?.ToString();
                    }
                    catch { /* body parse error — return raw info only */ }

                    return dto;
                }).ToList();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error peeking SQS messages");
                return StatusCode(500, new { message = $"Erro ao ler mensagens da fila: {ex.Message}" });
            }
        }

        [HttpPost("messages/process")]
        public async Task<IActionResult> ProcessMessage([FromBody] ProcessMessageRequest request)
        {
            if (string.IsNullOrEmpty(_queueUrl))
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
                await _sqs.DeleteMessageAsync(_queueUrl, request.ReceiptHandle);

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
        public async Task<IActionResult> DiscardMessage([FromBody] ProcessMessageRequest request)
        {
            if (string.IsNullOrEmpty(_queueUrl))
                return BadRequest(new { message = "SQS não configurado." });

            if (string.IsNullOrEmpty(request.ReceiptHandle))
                return BadRequest(new { message = "receiptHandle é obrigatório." });

            try
            {
                await _sqs.DeleteMessageAsync(_queueUrl, request.ReceiptHandle);
                return Ok(new { success = true, message = "Mensagem descartada da fila." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discarding SQS message");
                return StatusCode(500, new { message = $"Erro ao descartar mensagem: {ex.Message}" });
            }
        }
    }
}
