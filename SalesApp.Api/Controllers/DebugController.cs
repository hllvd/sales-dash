using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon;

namespace SalesApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // Allow testing without auth
    public class DebugController : ControllerBase
    {
        private readonly ILogger<DebugController> _logger;

        public DebugController(ILogger<DebugController> logger)
        {
            _logger = logger;
        }

        [HttpGet("throw")]
        public IActionResult ThrowError()
        {
            _logger.LogInformation("Debug: About to throw a test exception.");
            throw new Exception("This is a TEST EXCEPTION for CloudWatch logging verification.");
        }

        [HttpGet("log-error")]
        public IActionResult LogErrorOnly()
        {
            _logger.LogError("This is a TEST ERROR LOG for CloudWatch logging verification (no exception thrown).");
            return Ok(new { message = "Error logged to CloudWatch (if enabled)." });
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var cwEnabled = Environment.GetEnvironmentVariable("CW_ERROR_LOG")?.ToLower() == "true";
            var region = Environment.GetEnvironmentVariable("AWS__Region") ?? Environment.GetEnvironmentVariable("AWS_REGION");
            var group = Environment.GetEnvironmentVariable("CW_LOG_GROUP") ?? Environment.GetEnvironmentVariable("AWS__CloudWatchLogGroup");
            
            return Ok(new { 
                cloudWatchEnabled = cwEnabled,
                region = region,
                logGroup = group,
                hasAccessKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")),
                hasSecretKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")),
                message = "Debug controller is active."
            });
        }

        [HttpGet("test-aws-direct")]
        public async Task<IActionResult> TestAwsDirect()
        {
            var region = Environment.GetEnvironmentVariable("AWS__Region") ?? Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            var group = Environment.GetEnvironmentVariable("CW_LOG_GROUP") ?? Environment.GetEnvironmentVariable("AWS__CloudWatchLogGroup") ?? "/salesapp/api/errors";
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

            try
            {
                AmazonCloudWatchLogsClient client;
                if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                {
                    client = new AmazonCloudWatchLogsClient(accessKey, secretKey, RegionEndpoint.GetBySystemName(region));
                }
                else
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.GetBySystemName(region));
                }

                var streamName = $"test-stream-{Guid.NewGuid()}";
                
                // 1. Try to create log group (might already exist)
                try { await client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = group }); }
                catch (ResourceAlreadyExistsException) { }

                // 2. Try to create log stream
                await client.CreateLogStreamAsync(new CreateLogStreamRequest { LogGroupName = group, LogStreamName = streamName });

                // 3. Try to put a log event
                await client.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = group,
                    LogStreamName = streamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent
                        {
                            Message = $"Direct SDK Test Log at {DateTime.UtcNow}",
                            Timestamp = DateTime.UtcNow
                        }
                    }
                });

                return Ok(new { success = true, message = $"Successfully sent direct log to {group}/{streamName}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }
}
