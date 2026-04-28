using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace SalesApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // Only for testing!
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
            _logger.LogInformation("Diagnostics: About to throw a test exception.");
            throw new Exception("JSON LOGGING TEST: This is a test exception to verify structured logging in CloudWatch.");
        }

        [HttpGet("log-error")]
        public IActionResult LogErrorOnly()
        {
            _logger.LogError("JSON LOGGING TEST: This is a test error log. It should appear as a structured JSON object in Insights.");
            return Ok(new { message = "JSON Error logged to CloudWatch." });
        }
    }
}
