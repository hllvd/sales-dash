using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;
        
        public UserRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.ParentUser)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
        }
        
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        }

        public async Task<List<User>> GetByMatriculaAsync(string matricula)
        {
            return await _context.Users
                .Where(u => u.Matricula == matricula && u.IsActive)
                .Include(u => u.Role)
                .ToListAsync();
        }
        
        public async Task<(List<User> Users, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Users.AsQueryable();
            
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
            }
            
            var totalCount = await query.CountAsync();
            var users = await query
                .Include(u => u.ParentUser)
                .Include(u => u.Role)
                .OrderByDescending(u => u.IsActive)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            return (users, totalCount);
        }
        
        public async Task<User> CreateAsync(User user)
        {
            if (user.IsMatriculaOwner && !string.IsNullOrEmpty(user.Matricula))
            {
                var existingOwner = await _context.Users
                    .FirstOrDefaultAsync(u => u.Matricula == user.Matricula && u.IsMatriculaOwner && u.IsActive);
                
                if (existingOwner != null)
                {
                    throw new InvalidOperationException($"Matricula '{user.Matricula}' already has an owner.");
                }
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
        
        public async Task<User> UpdateAsync(User user)
        {
            if (user.IsMatriculaOwner && !string.IsNullOrEmpty(user.Matricula))
            {
                var existingOwner = await _context.Users
                    .FirstOrDefaultAsync(u => u.Matricula == user.Matricula && u.IsMatriculaOwner && u.IsActive && u.Id != user.Id);
                
                if (existingOwner != null)
                {
                    throw new InvalidOperationException($"Matricula '{user.Matricula}' already has an owner.");
                }
            }

            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }
        
        public async Task<bool> EmailExistsAsync(string email, Guid? excludeId = null)
        {
            return await _context.Users.AnyAsync(u => u.Email == email && (excludeId == null || u.Id != excludeId));
        }
        
        public async Task<List<User>> GetByRoleIdAsync(int roleId)
        {
            return await _context.Users
                .Where(u => u.RoleId == roleId && u.IsActive)
                .Include(u => u.ParentUser)
                .Include(u => u.Role)
                .ToListAsync();
        }
        
        public async Task<User?> GetParentAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.ParentUser)
                .FirstOrDefaultAsync(u => u.Id == userId);
            return user?.ParentUser;
        }
        
        public async Task<List<User>> GetChildrenAsync(Guid userId)
        {
            return await _context.Users
                .Where(u => u.ParentUserId == userId && u.IsActive)
                .Include(u => u.ParentUser)
                .ToListAsync();
        }
        
        public async Task<List<User>> GetTreeAsync(Guid userId, int depth = -1)
        {
            var result = new List<User>();
            await GetTreeRecursiveAsync(userId, depth, 0, result);
            return result;
        }
        
        private async Task GetTreeRecursiveAsync(Guid userId, int maxDepth, int currentDepth, List<User> result)
        {
            if (maxDepth != -1 && currentDepth > maxDepth) return;
            
            var user = await _context.Users
                .Include(u => u.ParentUser)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
                
            if (user == null) return;
            
            user.Level = currentDepth;
            result.Add(user);
            
            var children = await GetChildrenAsync(userId);
            foreach (var child in children)
            {
                await GetTreeRecursiveAsync(child.Id, maxDepth, currentDepth + 1, result);
            }
        }
        
        public async Task<int> GetLevelAsync(Guid userId)
        {
            var level = 0;
            var currentUserId = userId;
            
            while (true)
            {
                var parent = await GetParentAsync(currentUserId);
                if (parent == null) break;
                
                level++;
                currentUserId = parent.Id;
                
                // Prevent infinite loops
                if (level > 100) throw new InvalidOperationException("Circular reference detected in user hierarchy");
            }
            
            return level;
        }
        
        public async Task<User?> GetRootUserAsync()
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.ParentUserId == null && u.IsActive);
        }
        
        public async Task<bool> HasRootUserAsync()
        {
            return await _context.Users.AnyAsync(u => u.ParentUserId == null && u.IsActive);
        }
        
        public async Task<bool> WouldCreateCycleAsync(Guid userId, Guid? newParentId)
        {
            if (newParentId == null) return false;
            if (userId == newParentId) return true;
            
            var currentId = newParentId.Value;
            var visited = new HashSet<Guid> { userId };
            
            while (true)
            {
                if (visited.Contains(currentId)) return true;
                
                var parent = await GetParentAsync(currentId);
                if (parent == null) break;
                
                visited.Add(currentId);
                currentId = parent.Id;
            }
            
            return false;
        }
    }
}