using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;
        
        public RoleRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<Role?> GetByIdAsync(int id)
        {
            return await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == id);
        }
        
        public async Task<Role?> GetByNameAsync(string name)
        {
            return await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Name == name);
        }
        
        public async Task<List<Role>> GetAllAsync()
        {
            return await _context.Roles.Where(r => r.IsActive).OrderBy(r => r.Level).ToListAsync();
        }
        
        public async Task<Role> CreateAsync(Role role)
        {
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
            return role;
        }
        
        public async Task<Role> UpdateAsync(Role role)
        {
            role.UpdatedAt = DateTime.UtcNow;
            _context.Roles.Update(role);
            await _context.SaveChangesAsync();
            return role;
        }
        
        public async Task DeleteAsync(int id)
        {
            var role = await GetByIdAsync(id);
            if (role != null)
            {
                role.IsActive = false;
                await UpdateAsync(role);
            }
        }
        
        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            return await _context.Roles.AnyAsync(r => r.Name == name && r.IsActive && (!excludeId.HasValue || r.Id != excludeId.Value));
        }
    }
}