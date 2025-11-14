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

            if (!context.Roles.Any())
            {
                var roles = new[]
                {
                    new SalesApp.Models.Role { Name = "superadmin", Description = "Super Admin", Level = 1, IsActive = true },
                    new SalesApp.Models.Role { Name = "admin", Description = "Admin", Level = 2, IsActive = true },
                    new SalesApp.Models.Role { Name = "user", Description = "User", Level = 3, IsActive = true }
                };
                context.Roles.AddRange(roles);
                await context.SaveChangesAsync();
            }

            if (!context.Users.Any())
            {
                var adminRole = context.Roles.First(r => r.Name == "admin");
                var adminUser = new SalesApp.Models.User
                {
                    Name = "Admin User",
                    Email = "admin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = adminRole.Id,
                    IsActive = true
                };
                context.Users.Add(adminUser);
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