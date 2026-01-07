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
                    Name = "Super Admin",
                    Email = "superadmin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = (int)Models.RoleId.SuperAdmin,
                    IsActive = true
                };
                
                context.Users.Add(superAdminUser);
            }
            
            await context.SaveChangesAsync();
        }
    }
}