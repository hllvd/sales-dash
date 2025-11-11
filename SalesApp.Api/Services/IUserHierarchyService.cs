using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IUserHierarchyService
    {
        Task<User?> GetParentAsync(Guid userId);
        Task<List<User>> GetChildrenAsync(Guid userId);
        Task<List<User>> GetTreeAsync(Guid userId, int depth = -1);
        Task<int> GetLevelAsync(Guid userId);
        Task<User?> GetRootUserAsync();
        Task<string?> ValidateHierarchyChangeAsync(Guid userId, Guid? newParentId);
    }
}