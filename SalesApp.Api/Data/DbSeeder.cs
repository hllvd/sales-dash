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
            
            await context.SaveChangesAsync();
        }
    }
}