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
            // Prevent N+1 and Over-selecting (SELECT *) by ONLY downloading the Id, InternalId and ParentUserId.
            // NOTE: Inactive users are included so their contracts remain visible to superiors in the hierarchy.
            var allHierarchyLinks = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.InternalId, u.ParentUserId })
                .ToListAsync();

            var idToInternalId = allHierarchyLinks.ToDictionary(x => x.Id, x => x.InternalId);

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
            var allowedUserInternalIds = new HashSet<int>();
            if (idToInternalId.TryGetValue(currentUserId, out var currentInternalId))
            {
                allowedUserInternalIds.Add(currentInternalId);
            }

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
                            if (idToInternalId.TryGetValue(childId, out var childInternalId))
                            {
                                allowedUserInternalIds.Add(childInternalId);
                            }
                            queue.Enqueue(childId);
                        }
                    }
                }
            }

            context.AllowedUserIds = allowedUserIds;

            // Fetch teams managed or owned by hierarchy members
            var allowedTeamIds = await _context.Teams
                .AsNoTracking()
                .Where(t => t.IsActive && (
                    (t.OwnerUserInternalId.HasValue && allowedUserInternalIds.Contains(t.OwnerUserInternalId.Value)) ||
                    t.UserTeams.Any(ut => allowedUserInternalIds.Contains(ut.UserInternalId))
                ))
                .Select(t => t.Id)
                .Distinct()
                .ToListAsync();

            context.AllowedTeamIds = new HashSet<int>(allowedTeamIds);

            // Fetch Matricula numbers associated with these AllowedUserIds in ONE efficient query
            var now = DateTime.UtcNow;
            
            // Execute in batches if there are thousands of users, but usually it's fine for small/medium sets.
            // Using Contains which translates directly to IN ( ... )
            var allowedMatriculas = await _context.UserMatriculas
                .AsNoTracking()
                .Where(m => m.IsActive && 
                            (m.EndDate == null || m.EndDate > now) &&
                            allowedUserIds.Contains(m.User.Id))
                .Select(m => m.Matricula.MatriculaNumber)
                .Distinct()
                .ToListAsync();

            context.AllowedMatriculas = new HashSet<string>(allowedMatriculas);

            // Fetch Matricula numbers directly linked to the requesting admin (both owned and member)
            var adminMatriculas = await _context.UserMatriculas
                .AsNoTracking()
                .Where(m => m.IsActive &&
                            (m.EndDate == null || m.EndDate > now) &&
                            m.User.Id == currentUserId)
                .Select(m => new { m.Matricula.MatriculaNumber, m.IsOwner })
                .Distinct()
                .ToListAsync();

            context.AdminOwnedMatriculas = new HashSet<string>(adminMatriculas.Where(m => m.IsOwner).Select(m => m.MatriculaNumber));
            context.AdminLinkedMatriculas = new HashSet<string>(adminMatriculas.Select(m => m.MatriculaNumber));

            return context;
        }
    }
}
