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
        private readonly string _dbFileName;
        public HttpClient Client { get; }
        public IServiceProvider Services => _server.Services;

        public TestWebApplicationFactory()
        {
            _dbFileName = $"test_db_{Guid.NewGuid()}.db";

            var hostBuilder = new WebHostBuilder()
                .UseTestServer()
                .UseStartup<TestStartup>()
                .UseEnvironment("Testing")
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "ConnectionStrings:DefaultConnection", $"Data Source={_dbFileName}" }
                    });
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

            if (!context.Groups.Any(g => g.Id == 0))
            {
                // Try raw SQL via ADO.NET to force ID 0
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "INSERT OR IGNORE INTO Groups (Id, Name, Description, Commission, IsActive, CreatedAt, UpdatedAt) VALUES (0, 'Padrão', 'Grupo Padrão', 0, 1, datetime('now'), datetime('now'))";
                await command.ExecuteNonQueryAsync();
            }

            if (!context.Users.Any())
            {
                var superAdminRole = context.Roles.First(r => r.Name == "superadmin");
                var adminRole = context.Roles.First(r => r.Name == "admin");
                var userRole = context.Roles.First(r => r.Name == "user");
                
                var superAdminUser = new SalesApp.Models.User
                {
                    Name = "Super Admin User",
                    Email = "superadmin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("superadmin123"),
                    RoleId = superAdminRole.Id,
                    IsActive = true
                };
                
                var adminUser = new SalesApp.Models.User
                {
                    Name = "Admin User",
                    Email = "admin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = adminRole.Id,
                    IsActive = true
                };
                
                var regularUser = new SalesApp.Models.User
                {
                    Name = "Regular User",
                    Email = "user@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                    RoleId = userRole.Id,
                    IsActive = true
                };
                
                context.Users.AddRange(superAdminUser, adminUser, regularUser);
                await context.SaveChangesAsync();
            }

            // Seed permissions if none exist
            if (!context.Permissions.Any())
            {
                var perms = new List<SalesApp.Models.Permission>
                {
                    new SalesApp.Models.Permission { Name = "users:read", Description = "Read users" },
                    new SalesApp.Models.Permission { Name = "users:create", Description = "Create users" },
                    new SalesApp.Models.Permission { Name = "users:update", Description = "Update users" },
                    new SalesApp.Models.Permission { Name = "users:delete", Description = "Delete users" },
                    new SalesApp.Models.Permission { Name = "contracts:read", Description = "Read contracts" },
                    new SalesApp.Models.Permission { Name = "contracts:create", Description = "Create contracts" },
                    new SalesApp.Models.Permission { Name = "contracts:update", Description = "Update contracts" },
                    new SalesApp.Models.Permission { Name = "contracts:delete", Description = "Delete contracts" },
                    new SalesApp.Models.Permission { Name = "pvs:read", Description = "Read PVs" },
                    new SalesApp.Models.Permission { Name = "pvs:create", Description = "Create PVs" },
                    new SalesApp.Models.Permission { Name = "pvs:update", Description = "Update PVs" },
                    new SalesApp.Models.Permission { Name = "pvs:delete", Description = "Delete PVs" },
                    new SalesApp.Models.Permission { Name = "groups:read", Description = "Read groups" },
                    new SalesApp.Models.Permission { Name = "groups:write", Description = "Write groups" },
                    new SalesApp.Models.Permission { Name = "roles:read", Description = "Read roles" },
                    new SalesApp.Models.Permission { Name = "roles:create", Description = "Create roles" },
                    new SalesApp.Models.Permission { Name = "roles:update", Description = "Update roles" },
                    new SalesApp.Models.Permission { Name = "roles:delete", Description = "Delete roles" },
                    new SalesApp.Models.Permission { Name = "matriculas:read", Description = "Read matriculas" },
                    new SalesApp.Models.Permission { Name = "matriculas:write", Description = "Write matriculas" },
                    new SalesApp.Models.Permission { Name = "imports:execute", Description = "Execute imports" },
                    new SalesApp.Models.Permission { Name = "imports:history", Description = "View import history" },
                    new SalesApp.Models.Permission { Name = "imports:rollback", Description = "Rollback imports" },
                    new SalesApp.Models.Permission { Name = "system:admin", Description = "Admin access" },
                    new SalesApp.Models.Permission { Name = "system:superadmin", Description = "Super admin access" }
                };
                context.Permissions.AddRange(perms);
                await context.SaveChangesAsync();

                // Assign all to superadmin
                var superAdminRole = context.Roles.First(r => r.Name == "superadmin");
                foreach (var p in perms)
                {
                    context.RolePermissions.Add(new SalesApp.Models.RolePermission { RoleId = superAdminRole.Id, PermissionId = p.Id });
                }

                // Assign to Admin (matching test expectations)
                var adminRole = context.Roles.First(r => r.Name == "admin");
                var adminPerms = perms.Where(p => 
                    p.Name != "users:delete" && 
                    p.Name != "imports:rollback" && 
                    p.Name != "system:superadmin" &&
                    p.Name != "roles:delete" &&
                    !p.Name.StartsWith("pvs:")
                ).ToList();
                foreach (var p in adminPerms)
                {
                    context.RolePermissions.Add(new SalesApp.Models.RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });
                }

                await context.SaveChangesAsync();
            }
            
            // Seed PVs for testing
            if (!context.PVs.Any())
            {
                var pvs = new[]
                {
                    new SalesApp.Models.PV { Id = 1, Name = "Loja Centro", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new SalesApp.Models.PV { Id = 2, Name = "Loja Norte", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                };
                context.PVs.AddRange(pvs);
                await context.SaveChangesAsync();
            }

            // 🚀 Initialize RBAC Cache for Tests
            var rbacCache = scope.ServiceProvider.GetRequiredService<SalesApp.Services.IRbacCache>();
            var rolePerms = await context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();

            var cacheData = rolePerms.ToDictionary(
                r => r.Id,
                r => r.RolePermissions
                    .Select(rp => rp.Permission?.Name)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToHashSet()
            );

            rbacCache.Initialize(cacheData);
        }

        public void Dispose()
        {
            Client?.Dispose();
            _server?.Dispose();
            
            // Clean up the database file
            if (File.Exists(_dbFileName))
            {
                try
                {
                    File.Delete(_dbFileName);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}