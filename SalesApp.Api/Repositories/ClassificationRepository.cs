using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ClassificationLevelRepository : IClassificationLevelRepository
    {
        private readonly AppDbContext _context;

        public ClassificationLevelRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClassificationLevel?> GetByIdAsync(int id)
        {
            return await _context.ClassificationLevels
                .Include(l => l.NextLevel)
                .Include(l => l.MinimumDirect1Level)
                .Include(l => l.MinimumDirect2Level)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<List<ClassificationLevel>> GetAllAsync()
        {
            var levels = await _context.ClassificationLevels
                .Include(l => l.NextLevel)
                .Include(l => l.MinimumDirect1Level)
                .Include(l => l.MinimumDirect2Level)
                .ToListAsync();

            // Sort topologically: levels pointing to another level (NextLevelId) should appear before their targets.
            // i.e., level below first, higher level last.
            try
            {
                var levelMap = levels.ToDictionary(l => l.Id);
                var inDegree = levels.ToDictionary(l => l.Id, _ => 0);
                var adj = levels.ToDictionary(l => l.Id, _ => new List<int>());

                foreach (var l in levels)
                {
                    if (l.NextLevelId.HasValue && levelMap.ContainsKey(l.NextLevelId.Value))
                    {
                        int from = l.Id;
                        int to = l.NextLevelId.Value;
                        adj[from].Add(to);
                        inDegree[to]++;
                    }
                }

                // Initialize queue with root nodes (levels that are not anyone's NextLevel),
                // ordered by SalesGoal then Name for a stable start.
                var queue = new Queue<int>(levels
                    .Where(l => inDegree[l.Id] == 0)
                    .OrderBy(l => l.SalesGoal)
                    .ThenBy(l => l.Name)
                    .Select(l => l.Id));

                var sortedIds = new List<int>();

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();
                    sortedIds.Add(currentId);

                    // Visit next level(s)
                    var neighbors = adj[currentId]
                        .Select(nid => levelMap[nid])
                        .OrderBy(l => l.SalesGoal)
                        .ThenBy(l => l.Name)
                        .Select(l => l.Id)
                        .ToList();

                    foreach (var neighborId in neighbors)
                    {
                        inDegree[neighborId]--;
                        if (inDegree[neighborId] == 0)
                        {
                            queue.Enqueue(neighborId);
                        }
                    }
                }

                // If no cycles exist, return the sorted list
                if (sortedIds.Count == levels.Count)
                {
                    return sortedIds.Select(id => levelMap[id]).ToList();
                }
            }
            catch
            {
                // Fallback to default ordering on error
            }

            return levels
                .OrderBy(l => l.SalesGoal)
                .ThenBy(l => l.Name)
                .ToList();
        }

        public async Task<ClassificationLevel> CreateAsync(ClassificationLevel level)
        {
            level.CreatedAt = DateTime.UtcNow;
            level.UpdatedAt = DateTime.UtcNow;
            _context.ClassificationLevels.Add(level);
            await _context.SaveChangesAsync();
            return level;
        }

        public async Task<ClassificationLevel> UpdateAsync(ClassificationLevel level)
        {
            level.UpdatedAt = DateTime.UtcNow;
            _context.ClassificationLevels.Update(level);
            await _context.SaveChangesAsync();
            return level;
        }

        public async Task DeleteAsync(int id)
        {
            var level = await _context.ClassificationLevels.FindAsync(id);
            if (level != null)
            {
                var relatedClassifications = await _context.UserClassifications
                    .Where(uc => uc.LevelId == id)
                    .ToListAsync();
                _context.UserClassifications.RemoveRange(relatedClassifications);

                _context.ClassificationLevels.Remove(level);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            return await _context.ClassificationLevels
                .AnyAsync(l => l.Name.ToLower() == normalized && (excludeId == null || l.Id != excludeId));
        }

        public async Task<int> GetActiveUsersCountAsync(int levelId)
        {
            return await _context.UserClassifications
                .Include(uc => uc.User)
                .CountAsync(uc => uc.LevelId == levelId && uc.User != null && uc.User.IsActive && (uc.EndDate == null || uc.EndDate > DateTime.UtcNow));
        }
    }

    public class UserClassificationRepository : IUserClassificationRepository
    {
        private readonly AppDbContext _context;

        public UserClassificationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserClassification>> GetForUserAsync(int userInternalId)
        {
            return await _context.UserClassifications
                .Include(uc => uc.Level)
                .Where(uc => uc.UserInternalId == userInternalId)
                .OrderByDescending(uc => uc.StartDate)
                .ToListAsync();
        }

        public async Task<UserClassification?> GetActiveForUserAsync(int userInternalId)
        {
            return await _context.UserClassifications
                .Include(uc => uc.Level)
                .Where(uc => uc.UserInternalId == userInternalId
                             && (uc.EndDate == null || uc.EndDate > DateTime.UtcNow))
                .OrderByDescending(uc => uc.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<UserClassification>> GetForLevelAsync(int levelId)
        {
            return await _context.UserClassifications
                .Include(uc => uc.User)
                .Where(uc => uc.LevelId == levelId && uc.User != null && uc.User.IsActive)
                .OrderByDescending(uc => uc.StartDate)
                .ToListAsync();
        }

        public async Task<UserClassification?> GetByIdAsync(int id)
        {
            return await _context.UserClassifications
                .Include(uc => uc.Level)
                .Include(uc => uc.User)
                .FirstOrDefaultAsync(uc => uc.Id == id);
        }

        public async Task<UserClassification> CreateAsync(UserClassification classification)
        {
            classification.CreatedAt = DateTime.UtcNow;
            classification.UpdatedAt = DateTime.UtcNow;
            _context.UserClassifications.Add(classification);
            await _context.SaveChangesAsync();
            return classification;
        }

        public async Task<UserClassification> UpdateAsync(UserClassification classification)
        {
            classification.UpdatedAt = DateTime.UtcNow;
            _context.UserClassifications.Update(classification);
            await _context.SaveChangesAsync();
            return classification;
        }
    }
}
