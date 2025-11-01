using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class SaleRepository : ISaleRepository
    {
        private readonly AppDbContext _context;
        
        public SaleRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<Sale?> GetByIdAsync(Guid id)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        
        public async Task<List<Sale>> GetAllAsync(Guid? userId = null, Guid? groupId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Sales
                .Include(s => s.User)
                .Include(s => s.Group)
                .Where(s => s.IsActive);
            
            if (userId.HasValue)
                query = query.Where(s => s.UserId == userId.Value);
                
            if (groupId.HasValue)
                query = query.Where(s => s.GroupId == groupId.Value);
                
            if (startDate.HasValue)
                query = query.Where(s => s.CreatedAt >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(s => s.CreatedAt <= endDate.Value);
            
            return await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Sale>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Group)
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<Sale> CreateAsync(Sale sale)
        {
            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(sale.Id) ?? sale;
        }
        
        public async Task<Sale> UpdateAsync(Sale sale)
        {
            sale.UpdatedAt = DateTime.UtcNow;
            _context.Sales.Update(sale);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(sale.Id) ?? sale;
        }
    }
}