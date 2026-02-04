using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class PVRepository : IPVRepository
    {
        private readonly AppDbContext _context;
        
        public PVRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<List<PV>> GetAllAsync()
        {
            return await _context.PVs
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
        
        public async Task<PV?> GetByIdAsync(int id)
        {
            return await _context.PVs.FindAsync(id);
        }
        
        public async Task<PV?> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return await _context.PVs.FirstOrDefaultAsync(p => p.Name.ToLower() == name.Trim().ToLower());
        }
        
        public async Task<PV> CreateAsync(PV pv)
        {
            pv.CreatedAt = DateTime.UtcNow;
            pv.UpdatedAt = DateTime.UtcNow;
            _context.PVs.Add(pv);
            await _context.SaveChangesAsync();
            return pv;
        }
        
        public async Task<PV> UpdateAsync(PV pv)
        {
            pv.UpdatedAt = DateTime.UtcNow;
            _context.PVs.Update(pv);
            await _context.SaveChangesAsync();
            return pv;
        }
        
        public async Task<bool> DeleteAsync(int id)
        {
            var pv = await GetByIdAsync(id);
            if (pv == null) return false;
            
            _context.PVs.Remove(pv);
            await _context.SaveChangesAsync();
            return true;
        }
        
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.PVs.AnyAsync(p => p.Id == id);
        }
    }
}
