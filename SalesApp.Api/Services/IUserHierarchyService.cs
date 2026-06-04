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

        /// <summary>
        /// Returns the set of GUIDs for the given user and ALL their descendants
        /// (BFS traversal of the ParentUserId hierarchy). Only includes active users.
        /// </summary>
        Task<HashSet<Guid>> GetDescendantIdsAsync(Guid userId);

        /// <summary>
        /// Returns the set of InternalIds for the given user and ALL their descendants
        /// (BFS traversal of the ParentUserId hierarchy). Only includes active users.
        /// </summary>
        Task<HashSet<int>> GetDescendantInternalIdsAsync(Guid userId);
    }
}