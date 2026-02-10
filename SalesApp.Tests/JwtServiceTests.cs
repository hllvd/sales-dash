using Microsoft.Extensions.Configuration;
using Moq;
using SalesApp.Models;
using SalesApp.Services;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly Mock<IServiceScope> _mockScope;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<SalesApp.Repositories.IRoleRepository> _mockRoleRepository;

        public JwtServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("ThisIsASecretKeyForTestingPurposesOnly123456789");
            
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            _mockScope = new Mock<IServiceScope>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockRoleRepository = new Mock<SalesApp.Repositories.IRoleRepository>();

            _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
            _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(SalesApp.Repositories.IRoleRepository)))
                .Returns(_mockRoleRepository.Object);

            _jwtService = new JwtService(_mockConfiguration.Object, _mockScopeFactory.Object);
        }

        [Fact]
        public async Task GenerateToken_WithPermissions_ShouldContainCorrectPermClaims()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@example.com",
                RoleId = 1
            };

            var role = new Role
            {
                Id = 1,
                Name = "user",
                RolePermissions = new List<RolePermission>
                {
                    new RolePermission { Permission = new Permission { Name = "users:read" } }
                }
            };

            _mockRoleRepository.Setup(x => x.GetByIdAsync(user.RoleId)).ReturnsAsync(role);

            // Act
            var token = await _jwtService.GenerateToken(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var permClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "perm");
            permClaim.Should().NotBeNull();
            permClaim!.Value.Should().Be("users:read");
            
            // Should NOT have role claim anymore
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "role");
            roleClaim.Should().BeNull();
        }

        [Fact]
        public async Task GenerateToken_ShouldContainAllRequiredClaims()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Name = "Test User",
                Email = "test@example.com",
                RoleId = 2
            };

            _mockRoleRepository.Setup(x => x.GetByIdAsync(user.RoleId)).ReturnsAsync(new Role { Id = 2, Name = "admin" });

            // Act
            var token = await _jwtService.GenerateToken(user);

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
        }

        [Fact]
        public async Task ValidateToken_WithValidToken_ShouldReturnClaimsPrincipal()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@example.com",
                RoleId = 1
            };
            
            _mockRoleRepository.Setup(x => x.GetByIdAsync(user.RoleId)).ReturnsAsync(new Role { Id = 1, Name = "user" });
            
            var token = await _jwtService.GenerateToken(user);

            // Act
            var principal = _jwtService.ValidateToken(token);

            // Assert
            principal.Should().NotBeNull();
            principal!.Identity!.IsAuthenticated.Should().BeTrue();
            
            var emailClaim = principal.FindFirst(ClaimTypes.Email);
            emailClaim.Should().NotBeNull();
            emailClaim!.Value.Should().Be("test@example.com");
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