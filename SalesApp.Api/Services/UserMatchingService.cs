using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class UserMatchingService : IUserMatchingService
    {
        private readonly AppDbContext _context;

        public UserMatchingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserResponse>> FindUserMatchesAsync(string name, string surname)
        {
            var normalizedName = name.Trim().ToLowerInvariant();
            var normalizedSurname = surname.Trim().ToLowerInvariant();

            // Find users with matching name patterns
            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive)
                .ToListAsync();

            var matches = users
                .Where(u =>
                {
                    var userName = u.Name.ToLowerInvariant();
                    
                    // Check if name contains both name and surname
                    var containsName = userName.Contains(normalizedName);
                    var containsSurname = userName.Contains(normalizedSurname);
                    var containsBoth = userName.Contains($"{normalizedName} {normalizedSurname}") ||
                                      userName.Contains($"{normalizedSurname} {normalizedName}");
                    
                    return containsBoth || (containsName && containsSurname);
                })
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role?.Name ?? "",
                    ParentUserId = u.ParentUserId,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .ToList();

            return matches;
        }

        public async Task<User> CreateUserFromImportAsync(string name, string surname, string email, Guid createdByUserId)
        {
            // Generate full name
            var fullName = $"{name} {surname}".Trim();
            
            // Generate default password (should be changed on first login)
            var defaultPassword = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!");
            
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = fullName,
                Email = email,
                PasswordHash = defaultPassword,
                RoleId = 3, // Default to user role
                ParentUserId = createdByUserId, // Set the importing user as parent
                IsActive = true,
                Level = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }
    }
}
