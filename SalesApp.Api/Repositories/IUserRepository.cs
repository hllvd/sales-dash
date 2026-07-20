using SalesApp.Models;

namespace SalesApp.Repositories
{
    /// <summary>Minimal projection used for hierarchy BFS traversal.</summary>
    public record UserHierarchyLink(Guid Id, int InternalId, Guid? ParentUserId);

    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByEmailForAuthAsync(string email);
        Task<(List<User> Users, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null, string? contractNumber = null, HashSet<Guid>? allowedUserIds = null, bool activeOnly = false);
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

        /// <summary>
        /// Returns a minimal projection of all active users for BFS hierarchy traversal.
        /// Fetches only Id, InternalId, and ParentUserId — avoids loading full User entities.
        /// </summary>
        Task<List<UserHierarchyLink>> GetAllHierarchyLinksAsync();
    }
}