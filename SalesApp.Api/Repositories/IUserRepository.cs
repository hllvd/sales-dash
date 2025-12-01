using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<List<User>> GetByMatriculaAsync(string matricula);
        Task<(List<User> Users, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<bool> EmailExistsAsync(string email, Guid? excludeId = null);
        Task<List<User>> GetByRoleIdAsync(int roleId);
        
        // Hierarchy methods
        Task<User?> GetParentAsync(Guid userId);
        Task<List<User>> GetChildrenAsync(Guid userId);
        Task<List<User>> GetTreeAsync(Guid userId, int depth = -1);
        Task<int> GetLevelAsync(Guid userId);
        Task<User?> GetRootUserAsync();
        Task<bool> HasRootUserAsync();
        Task<bool> WouldCreateCycleAsync(Guid userId, Guid? newParentId);
    }
}