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
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<List<ClassificationLevel>> GetAllAsync()
        {
            return await _context.ClassificationLevels
                .OrderBy(l => l.Name)
                .ToListAsync();
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
                .CountAsync(uc => uc.LevelId == levelId && (uc.EndDate == null || uc.EndDate > DateTime.UtcNow));
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
                .Where(uc => uc.LevelId == levelId)
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
