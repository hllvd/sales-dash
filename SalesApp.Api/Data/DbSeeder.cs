using SalesApp.Models;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();
            
            // Check if admin user exists by email
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@salesapp.com");
            if (adminUser == null)
            {
                adminUser = new User
                {
                    Id = Guid.Parse("32af666a-413e-4745-bd26-bb4c6fef4a72"),
                    Name = "Admin User",
                    Email = "admin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = 2, // Admin role
                    IsActive = true
                };
                
                context.Users.Add(adminUser);
            }
            else if (adminUser.RoleId != 2)
            {
                // Update existing admin user to have admin role
                adminUser.RoleId = 2;
                context.Users.Update(adminUser);
            }
            
            // Add test admin user
            var testAdminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "user@example.com");
            if (testAdminUser == null)
            {
                testAdminUser = new User
                {
                    Id = Guid.Parse("2e23a8b2-4ead-4138-879f-85f2e1c74422"),
                    Name = "Test Admin",
                    Email = "user@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = 2, // Admin role
                    IsActive = true
                };
                
                context.Users.Add(testAdminUser);
            }
            
            // Add superadmin user
            var superAdminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "superadmin@salesapp.com");
            if (superAdminUser == null)
            {
                superAdminUser = new User
                {
                    Id = Guid.Parse("080a0aea-4cbd-490f-9d6c-bc001391b005"),
                    Name = "Super Admin",
                    Email = "superadmin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = (int)Models.RoleId.SuperAdmin,
                    IsActive = true
                };
                
                context.Users.Add(superAdminUser);
            }
            
            // Seed ImportTemplates
            var usersTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "Users");
            if (usersTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 1,
                    Name = "Users",
                    EntityType = "User",
                    Description = "Template for importing users",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Name", "Email" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Surname", "Role", "ParentEmail", "SendEmail", "Matricula", "IsMatriculaOwner", "Password" }),
                    DefaultMappings = "{}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = superAdminUser?.Id ?? adminUser?.Id ?? Guid.Parse("080a0aea-4cbd-490f-9d6c-bc001391b005")
                });
            }
            
            var contractsTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "Contracts");
            if (contractsTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 2,
                    Name = "Contracts",
                    EntityType = "Contract",
                    Description = "Template for importing contracts",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "UserEmail", "TotalAmount" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "GroupId", "Status", "SaleStartDate", "SaleEndDate", "ContractType", "Quota", "PvId", "CustomerName", "Version" }),
                    DefaultMappings = "{}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = superAdminUser?.Id ?? adminUser?.Id ?? Guid.Parse("080a0aea-4cbd-490f-9d6c-bc001391b005")
                });
            }
            
            var dashboardTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "contractDashboard");
            if (dashboardTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 3,
                    Name = "contractDashboard",
                    EntityType = "Contract",
                    Description = "Template for contract dashboard import from Power BI",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "TotalAmount", "SaleStartDate", "GroupId", "Quota", "CustomerName" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Status", "PvId", "PvName", "Version", "TempMatricula", "Category", "PlanoVenda" }),
                    DefaultMappings = "{}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = superAdminUser?.Id ?? adminUser?.Id ?? Guid.Parse("080a0aea-4cbd-490f-9d6c-bc001391b005")
                });
            }
            
            await SeedPermissions(context);
            
            await context.SaveChangesAsync();
        }

        private static async Task SeedPermissions(AppDbContext context)
        {
            var permissions = new List<string>
            {
                "users:read", "users:create", "users:update", "users:delete", "users:profile-update", "users:reset-password",
                "contracts:read", "contracts:create", "contracts:update", "contracts:delete",
                "pvs:read", "pvs:create", "pvs:update", "pvs:delete",
                "imports:execute", "imports:history", "imports:rollback",
                "groups:read", "groups:write",
                "matriculas:read", "matriculas:write",
                "roles:read", "roles:create", "roles:update", "roles:delete",
                "system:admin", "system:superadmin"
            };

            foreach (var permName in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permName))
                {
                    context.Permissions.Add(new Permission { Name = permName, Description = $"Permission to {permName}" });
                }
            }
            await context.SaveChangesAsync();

            // Assign to SuperAdmin (All)
            var allPerms = await context.Permissions.ToListAsync();
            await AssignPermissionsToRole(context, (int)Models.RoleId.SuperAdmin, allPerms);

            // Assign to Admin (Most permissions except high-risk ones)
            var adminPerms = allPerms.Where(p => 
                p.Name != "users:delete" && 
                p.Name != "imports:rollback" && 
                p.Name != "system:superadmin" &&
                p.Name != "roles:delete" &&
                !p.Name.StartsWith("pvs:")
            ).ToList();
            await AssignPermissionsToRole(context, (int)Models.RoleId.Admin, adminPerms);

            // Assign to User (Basic)
            var userPerms = allPerms.Where(p => 
                p.Name == "contracts:read" || 
                p.Name == "users:profile-update" || 
                p.Name == "users:reset-password"
            ).ToList();
            await AssignPermissionsToRole(context, (int)Models.RoleId.User, userPerms);
        }

        private static async Task AssignPermissionsToRole(AppDbContext context, int roleId, List<Permission> permissions)
        {
            foreach (var perm in permissions)
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == perm.Id))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}