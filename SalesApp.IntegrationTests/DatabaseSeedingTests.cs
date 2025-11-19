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
            users.Should().HaveCount(3);
            
            var superAdminUser = users.First(u => u.Email == "superadmin@test.com");
            superAdminUser.Role.Name.Should().Be("superadmin");
            BCrypt.Net.BCrypt.Verify("superadmin123", superAdminUser.PasswordHash).Should().BeTrue();
            
            var adminUser = users.First(u => u.Email == "admin@test.com");
            adminUser.Role.Name.Should().Be("admin");
            BCrypt.Net.BCrypt.Verify("admin123", adminUser.PasswordHash).Should().BeTrue();
            
            var regularUser = users.First(u => u.Email == "user@test.com");
            regularUser.Role.Name.Should().Be("user");
            BCrypt.Net.BCrypt.Verify("user123", regularUser.PasswordHash).Should().BeTrue();
        }
    }
}