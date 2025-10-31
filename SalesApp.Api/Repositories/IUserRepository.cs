using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<(List<User> Users, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<bool> EmailExistsAsync(string email, Guid? excludeId = null);
    }
}