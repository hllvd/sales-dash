using Microsoft.AspNetCore.Mvc;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuildInfoController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public BuildInfoController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var filePath = Path.Combine(_env.ContentRootPath, "build-info.txt");

            string buildId;
            if (System.IO.File.Exists(filePath))
            {
                buildId = System.IO.File.ReadAllText(filePath).Trim();
            }
            else
            {
                // Fallback when running locally outside Docker
                buildId = $"local-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            return Ok(new { buildId });
        }
    }
}
