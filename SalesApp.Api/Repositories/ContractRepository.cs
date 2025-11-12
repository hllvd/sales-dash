using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly AppDbContext _context;
        
        public ContractRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<Contract?> GetByIdAsync(int id)
        {
            return await _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Group)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        
        public async Task<Contract?> GetByContractNumberAsync(string contractNumber)
        {
            return await _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Group)
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
        }
        
        public async Task<List<Contract>> GetAllAsync(Guid? userId = null, Guid? groupId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Group)
                .Where(c => c.IsActive);
            
            if (userId.HasValue)
                query = query.Where(c => c.UserId == userId.Value);
                
            if (groupId.HasValue)
                query = query.Where(c => c.GroupId == groupId.Value);
                
            if (startDate.HasValue)
                query = query.Where(c => c.CreatedAt >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(c => c.CreatedAt <= endDate.Value);
            
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Contract>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Group)
                .Where(c => c.UserId == userId && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<Contract> CreateAsync(Contract contract)
        {
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(contract.Id) ?? contract;
        }
        
        public async Task<Contract> UpdateAsync(Contract contract)
        {
            contract.UpdatedAt = DateTime.UtcNow;
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(contract.Id) ?? contract;
        }
    }
}