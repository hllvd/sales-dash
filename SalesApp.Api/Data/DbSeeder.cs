using SalesApp.Models;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.EnsureCreatedAsync();
            
            // Check if admin user exists
            if (!await context.Users.AnyAsync(u => u.Role == "admin"))
            {
                var adminUser = new User
                {
                    Name = "Admin User",
                    Email = "admin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = "admin",
                    IsActive = true
                };
                
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }
}