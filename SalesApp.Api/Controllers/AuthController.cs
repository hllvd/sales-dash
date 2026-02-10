using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Repositories;
using SalesApp.Services;
using BCrypt.Net;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        
        public AuthController(IUserRepository userRepository, IJwtService jwtService)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
        }
        
        [HttpPost("token")]
        public async Task<ActionResult> GetToken([FromForm] string username, [FromForm] string password, [FromForm] string grant_type)
        {
            if (grant_type != "password")
            {
                return BadRequest(new { error = "unsupported_grant_type" });
            }
            
            var user = await _userRepository.GetByEmailAsync(username);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return Unauthorized(new { error = "invalid_grant" });
            }
            
            var token = await _jwtService.GenerateToken(user);
            
            return Ok(new
            {
                access_token = token,
                token_type = "Bearer",
                expires_in = 604800 // 7 days in seconds
            });
        }
    }
}