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
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Surname", "Role", "ParentEmail", "SendEmail", "Matricula", "IsMatriculaOwner" }),
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
            
            await context.SaveChangesAsync();
        }
    }
}