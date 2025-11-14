using Microsoft.Extensions.Configuration;
using Moq;
using SalesApp.Models;
using SalesApp.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests
{
    public class JwtServiceTests
    {
        private readonly JwtService _jwtService;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public JwtServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("ThisIsASecretKeyForTestingPurposesOnly123456789");
            _jwtService = new JwtService(_mockConfiguration.Object);
        }

        [Fact]
        public void GenerateToken_WithUserRole_ShouldContainCorrectRoleClaim()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@example.com",
                Role = new Role { Name = "user" }
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("user");
        }

        [Fact]
        public void GenerateToken_WithAdminRole_ShouldContainCorrectRoleClaim()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Admin User",
                Email = "admin@example.com",
                Role = new Role { Name = "admin" }
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("admin");
        }

        [Fact]
        public void GenerateToken_WithSuperAdminRole_ShouldContainCorrectRoleClaim()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Super Admin",
                Email = "superadmin@example.com",
                Role = new Role { Name = "superadmin" }
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("superadmin");
        }

        [Fact]
        public void GenerateToken_WithNullRole_ShouldDefaultToUserRole()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@example.com",
                Role = null
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("user");
        }

        [Fact]
        public void GenerateToken_ShouldContainAllRequiredClaims()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Name = "Test User",
                Email = "test@example.com",
                Role = new Role { Name = "admin" }
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var claims = jsonToken.Claims.ToList();
            
            // Check NameIdentifier claim (nameid in JWT)
            var nameIdentifierClaim = claims.FirstOrDefault(c => c.Type == "nameid");
            nameIdentifierClaim.Should().NotBeNull();
            nameIdentifierClaim!.Value.Should().Be(userId.ToString());
            
            // Check Email claim
            var emailClaim = claims.FirstOrDefault(c => c.Type == "email");
            emailClaim.Should().NotBeNull();
            emailClaim!.Value.Should().Be("test@example.com");
            
            // Check Name claim (unique_name in JWT)
            var nameClaim = claims.FirstOrDefault(c => c.Type == "unique_name");
            nameClaim.Should().NotBeNull();
            nameClaim!.Value.Should().Be("Test User");
            
            // Check Role claim
            var roleClaim = claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("admin");
        }

        [Fact]
        public void ValidateToken_WithValidToken_ShouldReturnClaimsPrincipal()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@example.com",
                Role = new Role { Name = "user" }
            };
            
            var token = _jwtService.GenerateToken(user);

            // Act
            var principal = _jwtService.ValidateToken(token);

            // Assert
            principal.Should().NotBeNull();
            principal!.Identity!.IsAuthenticated.Should().BeTrue();
            
            var roleClaim = principal.FindFirst(ClaimTypes.Role);
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be("user");
        }

        [Fact]
        public void ValidateToken_WithInvalidToken_ShouldReturnNull()
        {
            // Arrange
            var invalidToken = "invalid.jwt.token";

            // Act
            var principal = _jwtService.ValidateToken(invalidToken);

            // Assert
            principal.Should().BeNull();
        }
    }
}