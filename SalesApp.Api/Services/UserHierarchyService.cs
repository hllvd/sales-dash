using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class UserHierarchyService : IUserHierarchyService
    {
        private readonly IUserRepository _userRepository;
        
        public UserHierarchyService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        
        public async Task<User?> GetParentAsync(Guid userId)
        {
            return await _userRepository.GetParentAsync(userId);
        }
        
        public async Task<List<User>> GetChildrenAsync(Guid userId)
        {
            return await _userRepository.GetChildrenAsync(userId);
        }
        
        public async Task<List<User>> GetTreeAsync(Guid userId, int depth = -1)
        {
            return await _userRepository.GetTreeAsync(userId, depth);
        }
        
        public async Task<int> GetLevelAsync(Guid userId)
        {
            return await _userRepository.GetLevelAsync(userId);
        }
        
        public async Task<User?> GetRootUserAsync()
        {
            return await _userRepository.GetRootUserAsync();
        }
        
        public async Task<string?> ValidateHierarchyChangeAsync(Guid userId, Guid? newParentId)
        {
            // Rule 1: Cannot set self as parent
            if (newParentId == userId)
                return "Um usuário não pode ser seu próprio superior";
            
            // Rule 2: Check for circular reference
            if (newParentId.HasValue && await _userRepository.WouldCreateCycleAsync(userId, newParentId))
                return "Esta alteração criaria uma referência circular na hierarquia";
            
            // Rule 3: Only one root user allowed
            if (newParentId == null)
            {
                var existingRoot = await _userRepository.GetRootUserAsync();
                if (existingRoot != null && existingRoot.Id != userId)
                    return "Apenas um usuário raiz (sem superior) é permitido no sistema";
            }
            
            // Rule 4: Parent must exist and be active
            if (newParentId.HasValue)
            {
                var parent = await _userRepository.GetByIdAsync(newParentId.Value);
                if (parent == null || !parent.IsActive)
                    return "O usuário superior não existe ou está inativo";
            }
            
            return null; // Valid
        }

        /// <inheritdoc />
        public async Task<HashSet<Guid>> GetDescendantIdsAsync(Guid userId)
        {
            var allLinks = await _userRepository.GetAllHierarchyLinksAsync();
            return BuildDescendantIds(userId, allLinks);
        }

        /// <inheritdoc />
        public async Task<HashSet<int>> GetDescendantInternalIdsAsync(Guid userId)
        {
            // Optimized: fetch only the minimal projection needed for BFS — no full User entities.
            var allLinks = await _userRepository.GetAllHierarchyLinksAsync();
            var descendants = BuildDescendantIds(userId, allLinks);
            var internalIdMap = allLinks.ToDictionary(l => l.Id, l => l.InternalId);

            return descendants
                .Where(id => internalIdMap.ContainsKey(id))
                .Select(id => internalIdMap[id])
                .ToHashSet();
        }

        private HashSet<Guid> BuildDescendantIds(Guid userId, List<UserHierarchyLink> allLinks)
        {
            // Build children map: parentId -> [childIds]
            var childrenMap = new Dictionary<Guid, List<Guid>>();

            foreach (var link in allLinks)
            {
                if (link.ParentUserId.HasValue)
                {
                    if (!childrenMap.ContainsKey(link.ParentUserId.Value))
                        childrenMap[link.ParentUserId.Value] = new List<Guid>();
                    childrenMap[link.ParentUserId.Value].Add(link.Id);
                }
            }

            // BFS from userId — includes the user themselves
            var descendants = new HashSet<Guid> { userId };
            var queue = new Queue<Guid>();
            queue.Enqueue(userId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (childrenMap.TryGetValue(current, out var children))
                {
                    foreach (var childId in children)
                    {
                        if (descendants.Add(childId))
                            queue.Enqueue(childId);
                    }
                }
            }

            return descendants;
        }
    }
}