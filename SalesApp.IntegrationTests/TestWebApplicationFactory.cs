using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using SalesApp.Data;
using System.Net.Http;
using System.Threading.Tasks;

namespace SalesApp.IntegrationTests
{
    public class TestWebApplicationFactory : IAsyncLifetime, IDisposable
    {
        private readonly TestServer _server;
        private readonly string _dbFileName;
        public HttpClient Client { get; }
        public HttpClient CreateClient() => _server.CreateClient();
        public IServiceProvider Services => _server.Services;

        private bool _dbReset = false;
        private bool _dbSchemaCreated = false;
        private readonly object _dbLock = new object();

        public TestWebApplicationFactory(string dbFileName = "SalesApp.IntegrationTests.db")
        {
            _dbFileName = dbFileName;

            lock (_dbLock)
            {
                if (!_dbReset)
                {
                    if (File.Exists(_dbFileName))
                    {
                        try { File.Delete(_dbFileName); } catch { /* Ignore */ }
                    }
                    _dbReset = true;
                }
            }


            var hostBuilder = new WebHostBuilder()
                .UseTestServer()
                .UseStartup<TestStartup>()
                .UseEnvironment("Testing")
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "ConnectionStrings:DefaultConnection", $"Data Source={_dbFileName};Default Timeout=15;" }
                    });
                });

            _server = new TestServer(hostBuilder);
            Client = _server.CreateClient();
        }

        public async Task<HttpClient> CreateClientWithServicesAsync(Action<IServiceCollection> configureServices)
        {
            var hostBuilder = new WebHostBuilder()
                .UseTestServer()
                .UseStartup<TestStartup>()
                .UseEnvironment("Testing")
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "ConnectionStrings:DefaultConnection", $"Data Source={_dbFileName};Default Timeout=15;" }
                    });
                })
                .ConfigureTestServices(configureServices);

            var server = new TestServer(hostBuilder);
            await SeedTestData(server.Services);
            return server.CreateClient();
        }

        public async Task InitializeAsync()
        {
            // Seed test data
            await SeedTestData(_server.Services);
        }

        public Task DisposeAsync() => Task.CompletedTask;
        
        private async Task SeedTestData(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            
            lock (_dbLock)
            {
                if (!_dbSchemaCreated)
                {
                    context.Database.EnsureCreated();
                    _dbSchemaCreated = true;
                }
            }

            // Seed ContractStatuses to satisfy FK constraints in tests
            if (!context.ContractStatuses.Any())
            {
                var statuses = new[]
                {
                    new SalesApp.Models.ContractStatusEntity { Id = 1, Name = "Active" },
                    new SalesApp.Models.ContractStatusEntity { Id = 2, Name = "Late1" },
                    new SalesApp.Models.ContractStatusEntity { Id = 3, Name = "Late2" },
                    new SalesApp.Models.ContractStatusEntity { Id = 4, Name = "Late3" },
                    new SalesApp.Models.ContractStatusEntity { Id = 5, Name = "Defaulted" },
                    new SalesApp.Models.ContractStatusEntity { Id = 6, Name = "Transferred" },
                    new SalesApp.Models.ContractStatusEntity { Id = 7, Name = "AwaitingPayment" }
                };
                context.ContractStatuses.AddRange(statuses);
                await context.SaveChangesAsync();
            }

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
                    new SalesApp.Models.Permission { Name = "users:profile-update", Description = "Update profile" },
                    new SalesApp.Models.Permission { Name = "users:reset-password", Description = "Reset password" },
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
                    new SalesApp.Models.Permission { Name = "teams:manage", Description = "Manage teams" },
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

                // Assign to User (Basic)
                var userRole = context.Roles.First(r => r.Name == "user");
                var userPerms = perms.Where(p => 
                    p.Name == "contracts:read" || 
                    p.Name == "users:profile-update" || 
                    p.Name == "users:reset-password"
                ).ToList();
                foreach (var p in userPerms)
                {
                    context.RolePermissions.Add(new SalesApp.Models.RolePermission { RoleId = userRole.Id, PermissionId = p.Id });
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

            // Seed ImportTemplates for permission tests
            if (!context.ImportTemplates.Any())
            {
                var admin = context.Users.First(u => u.Email == "superadmin@test.com");
                var templates = new SalesApp.Models.ImportTemplate[]
                {
                    new SalesApp.Models.ImportTemplate 
                    { 
                        Id = 1, 
                        Name = "Users", 
                        EntityType = "User", 
                        RequiredFields = "[\"Name\",\"Email\",\"Matricula\"]", 
                        OptionalFields = "[\"Surname\",\"Role\",\"ParentEmail\",\"SendEmail\",\"IsMatriculaOwner\",\"Password\"]", 
                        DefaultMappings = "{}", 
                        IsActive = true, 
                        CreatedByUserInternalId = admin.InternalId 
                    },
                    new SalesApp.Models.ImportTemplate 
                    { 
                        Id = 2, 
                        Name = "Contracts", 
                        EntityType = "Contract", 
                        RequiredFields = "[\"ContractNumber\",\"UserEmail\",\"TotalAmount\",\"MatriculaNumber\"]", 
                        OptionalFields = "[\"GroupId\",\"Status\",\"SaleStartDate\",\"SaleEndDate\",\"ContractType\",\"Quota\",\"PvId\",\"PvName\",\"CustomerName\",\"Version\"]", 
                        DefaultMappings = "{}", 
                        IsActive = true, 
                        CreatedByUserInternalId = admin.InternalId 
                    },
                    new SalesApp.Models.ImportTemplate 
                    { 
                        Id = 3, 
                        Name = "contractDashboard", 
                        EntityType = "Contract", 
                        RequiredFields = "[\"ContractNumber\",\"TotalAmount\",\"SaleStartDate\",\"GroupId\",\"Quota\",\"CustomerName\",\"MatriculaNumber\"]", 
                        OptionalFields = "[\"Status\",\"PvId\",\"PvName\",\"Version\",\"Category\",\"PlanoVenda\",\"UserEmail\"]", 
                        DefaultMappings = "{}", 
                        IsActive = true, 
                        CreatedByUserInternalId = admin.InternalId 
                    }
                };
                context.ImportTemplates.AddRange(templates);
                await context.SaveChangesAsync();
            }

            // Seed ContractStatuses
            if (!context.ContractStatuses.Any())
            {
                var statuses = new[]
                {
                    new SalesApp.Models.ContractStatusEntity { Id = 1, Name = "Active" },
                    new SalesApp.Models.ContractStatusEntity { Id = 2, Name = "Late1" },
                    new SalesApp.Models.ContractStatusEntity { Id = 3, Name = "Late2" },
                    new SalesApp.Models.ContractStatusEntity { Id = 4, Name = "Late3" },
                    new SalesApp.Models.ContractStatusEntity { Id = 5, Name = "Defaulted" },
                    new SalesApp.Models.ContractStatusEntity { Id = 6, Name = "Transferred" },
                    new SalesApp.Models.ContractStatusEntity { Id = 7, Name = "AwaitingPayment" }
                };
                context.ContractStatuses.AddRange(statuses);
                await context.SaveChangesAsync();
            }

            // Seed ClassificationLevels
            if (!context.ClassificationLevels.Any())
            {
                var levels = new[]
                {
                    new SalesApp.Models.ClassificationLevel { Id = 1, Name = "Bronze", Description = "Nível Bronze", SalesGoal = 10000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new SalesApp.Models.ClassificationLevel { Id = 2, Name = "Prata", Description = "Nível Prata", SalesGoal = 30000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new SalesApp.Models.ClassificationLevel { Id = 3, Name = "Ouro", Description = "Nível Ouro", SalesGoal = 60000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                };
                context.ClassificationLevels.AddRange(levels);
                await context.SaveChangesAsync();
            }

            // Seed UserMetadataFields
            if (!context.UserMetadataFields.Any())
            {
                var fields = new[]
                {
                    new SalesApp.Models.UserMetadataField { Key = "secretary_name", Label = "Nome da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new SalesApp.Models.UserMetadataField { Key = "secretary_email", Label = "E-mail da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new SalesApp.Models.UserMetadataField { Key = "secretary_whatsapp", Label = "WhatsApp da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow }
                };
                context.UserMetadataFields.AddRange(fields);
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
        }
    }
}