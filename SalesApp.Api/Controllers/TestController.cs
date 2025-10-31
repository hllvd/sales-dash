using Microsoft.AspNetCore.Mvc;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "SalesApp API is running!", timestamp = DateTime.UtcNow });
        }
    }
}