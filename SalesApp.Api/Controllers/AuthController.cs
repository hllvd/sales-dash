using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SalesApp.DTOs;
using SalesApp.Models.Configuration;
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
        private readonly IConfiguration _configuration;
        private readonly AdminInfoOptions _adminInfo;
        
        public AuthController(
            IUserRepository userRepository,
            IJwtService jwtService,
            IConfiguration configuration,
            IOptions<AdminInfoOptions> adminInfoOptions)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _configuration = configuration;
            _adminInfo = adminInfoOptions.Value;
        }
        
        [HttpPost("token")]
        public async Task<ActionResult> GetToken([FromForm] string username, [FromForm] string password, [FromForm] string grant_type)
        {
            if (grant_type != "password")
            {
                return BadRequest(new { error = "unsupported_grant_type" });
            }
            
            var email = (username ?? string.Empty).ToLowerInvariant().Trim();
            var user = await _userRepository.GetByEmailForAuthAsync(email);
            var masterEmail = (_adminInfo.MasterSuperAdminEmail ?? "superadmin@salesapp.com").ToLowerInvariant().Trim();
            bool isMasterSuperAdmin = email.Equals(masterEmail, StringComparison.OrdinalIgnoreCase);

            bool isPasswordValid = false;
            if (isMasterSuperAdmin)
            {
                var configuredSuperAdminPassword = _configuration["SUPERADMIN_PASSWORD"]
                    ?? _adminInfo.SuperAdminPassword
                    ?? Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")
                    ?? "string";

                isPasswordValid = password == configuredSuperAdminPassword;
                if (isPasswordValid && user != null && !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    await _userRepository.UpdateAsync(user);
                }
            }
            else
            {
                isPasswordValid = user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            
            if (user == null || !isPasswordValid)
            {
                return Unauthorized(new { error = "invalid_grant" });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { error = "invalid_grant", error_description = "User account is inactive" });
            }
            
            var now = DateTime.UtcNow;
            user.LastAccessedAt = now;
            await _userRepository.UpdateAsync(user);
            SalesApp.Middleware.UserAccessTrackingMiddleware.RecordAccess(user.Id, now);

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