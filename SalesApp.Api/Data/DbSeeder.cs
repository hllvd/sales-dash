using SalesApp.Models;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();
            
            // Check if admin user exists
            if (!await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
            {
                var adminUser = new User
                {
                    Name = "Admin User",
                    Email = "admin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = UserRole.Admin,
                    IsActive = true
                };
                
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }
}