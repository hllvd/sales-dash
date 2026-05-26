using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface ITeamRepository
    {
        Task<Team?> GetByIdAsync(int id);
        Task<Team?> GetByNameAsync(string name);
        Task<List<Team>> GetAllAsync();
        Task<Team> CreateAsync(Team team);
        Task<Team> UpdateAsync(Team team);
        Task DeleteAsync(int id);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
        
        Task<List<UserTeam>> GetActiveMembershipsForUserAsync(int userInternalId, DateTime at);
        Task<List<UserTeam>> FindOverlappingMembershipsAsync(int userInternalId, DateTime start, DateTime? end);
        Task<UserTeam> AddMemberAsync(UserTeam userTeam);
        Task RemoveMemberAsync(int teamId, int userInternalId);
        Task SetOwnerAsync(int teamId, int? ownerUserInternalId);
    }
}
