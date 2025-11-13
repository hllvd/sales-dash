using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class GroupRepository : IGroupRepository
    {
        private readonly AppDbContext _context;
        
        public GroupRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<Group?> GetByIdAsync(int id)
        {
            return await _context.Groups.FindAsync(id);
        }
        
        public async Task<List<Group>> GetAllAsync()
        {
            return await _context.Groups.Where(g => g.IsActive).ToListAsync();
        }
        
        public async Task<Group> CreateAsync(Group group)
        {
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }
        
        public async Task<Group> UpdateAsync(Group group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            _context.Groups.Update(group);
            await _context.SaveChangesAsync();
            return group;
        }
        
        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            return await _context.Groups.AnyAsync(g => g.Name == name && g.IsActive && (excludeId == null || g.Id != excludeId));
        }
    }
}