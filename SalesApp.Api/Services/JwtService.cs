using Microsoft.IdentityModel.Tokens;
using SalesApp.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SalesApp.Services
{
    public interface IJwtService
    {
        Task<string> GenerateToken(User user);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
    }
    
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        
        public JwtService(IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }
        
        public async Task<string> GenerateToken(User user)
        {
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
            var expirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "1440");

            // Load permissions from DB (expensive, done once per login)
            var permissions = new List<string>();
            using (var scope = _scopeFactory.CreateScope())
            {
                var roleRepo = scope.ServiceProvider.GetRequiredService<SalesApp.Repositories.IRoleRepository>();
                var role = await roleRepo.GetByIdAsync(user.RoleId);
                if (role != null)
                {
                    permissions = role.RolePermissions
                        .Select(rp => rp.Permission?.Name)
                        .Where(name => name != null)
                        .Cast<string>()
                        .ToList();
                }
            }
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("role_id", user.RoleId.ToString())
            };

            // Add permissions to JWT
            foreach (var perm in permissions)
            {
                claims.Add(new Claim("perm", perm));
            }
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
        
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
                var tokenHandler = new JwtSecurityTokenHandler();
                
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
                
                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}