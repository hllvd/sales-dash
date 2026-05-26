using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly AppDbContext _context;

        public TeamRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            return await _context.Teams
                .Include(t => t.Owner)
                .Include(t => t.UserTeams)
                    .ThenInclude(ut => ut.User)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Team?> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return await _context.Teams
                .Include(t => t.Owner)
                .Include(t => t.UserTeams)
                    .ThenInclude(ut => ut.User)
                .FirstOrDefaultAsync(t => t.Name.ToLower() == name.Trim().ToLower());
        }

        public async Task<List<Team>> GetAllAsync(HashSet<int>? allowedOwnerInternalIds = null)
        {
            var query = _context.Teams
                .Include(t => t.Owner)
                .Include(t => t.UserTeams)
                    .ThenInclude(ut => ut.User)
                .AsQueryable();

            if (allowedOwnerInternalIds != null)
            {
                query = query.Where(t => t.OwnerUserInternalId != null && allowedOwnerInternalIds.Contains(t.OwnerUserInternalId.Value));
            }

            return await query.OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<Team> CreateAsync(Team team)
        {
            team.CreatedAt = DateTime.UtcNow;
            team.UpdatedAt = DateTime.UtcNow;
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
            return team;
        }

        public async Task<Team> UpdateAsync(Team team)
        {
            team.UpdatedAt = DateTime.UtcNow;
            _context.Teams.Update(team);
            await _context.SaveChangesAsync();
            return team;
        }

        public async Task DeleteAsync(int id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team != null)
            {
                // If this team is being deleted, remove it. EF will cascade delete UserTeams.
                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            return await _context.Teams
                .AnyAsync(t => t.Name.ToLower() == normalized && (excludeId == null || t.Id != excludeId));
        }

        public async Task<List<UserTeam>> GetActiveMembershipsForUserAsync(int userInternalId, DateTime at)
        {
            return await _context.UserTeams
                .Include(ut => ut.Team)
                .Where(ut => ut.UserInternalId == userInternalId && 
                             ut.StartDate <= at && 
                             (ut.EndDate == null || ut.EndDate > at))
                .ToListAsync();
        }

        public async Task<List<UserTeam>> FindOverlappingMembershipsAsync(int userInternalId, DateTime start, DateTime? end)
        {
            // Overlap formula: A overlaps B if (StartA < EndB or EndB is null) AND (EndA is null or EndA > StartB)
            return await _context.UserTeams
                .Include(ut => ut.Team)
                .Where(ut => ut.UserInternalId == userInternalId &&
                             ut.StartDate < (end ?? DateTime.MaxValue) &&
                             (ut.EndDate == null || ut.EndDate > start))
                .ToListAsync();
        }

        public async Task<UserTeam> AddMemberAsync(UserTeam userTeam)
        {
            userTeam.CreatedAt = DateTime.UtcNow;
            userTeam.UpdatedAt = DateTime.UtcNow;
            _context.UserTeams.Add(userTeam);
            await _context.SaveChangesAsync();
            return userTeam;
        }

        public async Task RemoveMemberAsync(int teamId, int userInternalId)
        {
            var activeLinks = await _context.UserTeams
                .Where(ut => ut.TeamId == teamId && ut.UserInternalId == userInternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                .ToListAsync();

            foreach (var link in activeLinks)
            {
                link.EndDate = DateTime.UtcNow;
                link.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task SetOwnerAsync(int teamId, int? ownerUserInternalId)
        {
            var team = await _context.Teams.FindAsync(teamId);
            if (team != null)
            {
                team.OwnerUserInternalId = ownerUserInternalId;
                team.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
