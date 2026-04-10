using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class UserScopeService : IUserScopeService
    {
        private readonly AppDbContext _context;

        public UserScopeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserScopeContext> GetContractScopeAsync(ClaimsPrincipal user)
        {
            var context = new UserScopeContext();
            
            // Unauthenticated
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return context;

            var roleIdClaim = user.FindFirst("role_id")?.Value;
                
            // RoleId 1 represents superadmin in our system
            if (roleIdClaim == "1")
            {
                context.IsGlobal = true;
                return context;
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var currentUserId))
                return context;

            // PERFORMANCE OPTIMIZATION:
            // Prevent N+1 and Over-selecting (SELECT *) by ONLY downloading the Id and ParentUserId of active users.
            var allHierarchyLinks = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.ParentUserId })
                .ToListAsync();

            // Build dictionary for fast O(1) adjacency list lookup
            var childrenMap = new Dictionary<Guid, List<Guid>>();
            foreach (var link in allHierarchyLinks)
            {
                if (link.ParentUserId.HasValue)
                {
                    if (!childrenMap.ContainsKey(link.ParentUserId.Value))
                        childrenMap[link.ParentUserId.Value] = new List<Guid>();
                    
                    childrenMap[link.ParentUserId.Value].Add(link.Id);
                }
            }

            // Traverse to gather all descendant distinct IDs including the admin themselves
            var allowedUserIds = new HashSet<Guid> { currentUserId };
            var queue = new Queue<Guid>();
            queue.Enqueue(currentUserId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (childrenMap.TryGetValue(current, out var children))
                {
                    foreach (var childId in children)
                    {
                        if (allowedUserIds.Add(childId))
                        {
                            queue.Enqueue(childId);
                        }
                    }
                }
            }

            context.AllowedUserIds = allowedUserIds;

            // Fetch Matricula numbers associated with these AllowedUserIds in ONE efficient query
            var now = DateTime.UtcNow;
            
            // Execute in batches if there are thousands of users, but usually it's fine for small/medium sets.
            // Using Contains which translates directly to IN ( ... )
            var allowedMatriculas = await _context.UserMatriculas
                .AsNoTracking()
                .Where(m => m.IsActive && 
                            (m.EndDate == null || m.EndDate > now) &&
                            allowedUserIds.Contains(m.UserId))
                .Select(m => m.MatriculaNumber)
                .Distinct()
                .ToListAsync();

            context.AllowedMatriculas = new HashSet<string>(allowedMatriculas);

            return context;
        }
    }
}
