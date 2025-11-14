using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;

namespace SalesApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class DatabaseSeedingTests
    {
        private readonly TestWebApplicationFactory _factory;

        public DatabaseSeedingTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Database_ShouldHaveSeededData()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var roles = await context.Roles.ToListAsync();
            var users = await context.Users.Include(u => u.Role).ToListAsync();

            roles.Should().HaveCount(3);
            users.Should().HaveCount(1);
            
            var adminUser = users.First();
            adminUser.Email.Should().Be("admin@test.com");
            adminUser.Role.Name.Should().Be("admin");
            
            // Test password verification
            var isValidPassword = BCrypt.Net.BCrypt.Verify("admin123", adminUser.PasswordHash);
            isValidPassword.Should().BeTrue();
        }
    }
}