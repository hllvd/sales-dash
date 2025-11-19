using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.IntegrationTests
{
    public class TestWebApplicationFactory : IDisposable
    {
        private readonly TestServer _server;
        public HttpClient Client { get; }
        public IServiceProvider Services => _server.Services;

        public TestWebApplicationFactory()
        {
            var hostBuilder = new WebHostBuilder()
                .UseTestServer()
                .UseStartup<TestStartup>()
                .UseEnvironment("Testing")
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                });

            _server = new TestServer(hostBuilder);
            Client = _server.CreateClient();
            
            // Seed test data immediately
            SeedTestData().Wait();
        }
        
        private async Task SeedTestData()
        {
            using var scope = _server.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            
            context.Database.EnsureCreated();

            // Seed roles with explicit IDs to match the migration
            if (!context.Roles.Any())
            {
                var roles = new[]
                {
                    new SalesApp.Models.Role { Id = 1, Name = "superadmin", Description = "Super Administrator with full system access", Level = 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new SalesApp.Models.Role { Id = 2, Name = "admin", Description = "Administrator with management access", Level = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new SalesApp.Models.Role { Id = 3, Name = "user", Description = "Regular user with basic access", Level = 3, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                };
                context.Roles.AddRange(roles);
                await context.SaveChangesAsync();
            }

            if (!context.Users.Any())
            {
                var adminRole = context.Roles.First(r => r.Name == "admin");
                var superAdminRole = context.Roles.First(r => r.Name == "superadmin");
                
                var adminUser = new SalesApp.Models.User
                {
                    Name = "Admin User",
                    Email = "admin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = adminRole.Id,
                    IsActive = true
                };
                
                var superAdminUser = new SalesApp.Models.User
                {
                    Name = "Super Admin User",
                    Email = "superadmin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("superadmin123"),
                    RoleId = superAdminRole.Id,
                    IsActive = true
                };
                
                context.Users.AddRange(adminUser, superAdminUser);
                await context.SaveChangesAsync();
            }
        }

        public void Dispose()
        {
            Client?.Dispose();
            _server?.Dispose();
        }
    }
}