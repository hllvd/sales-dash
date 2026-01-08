using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
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
            // NOTE: No AsNoTracking - used after create/update, needs tracked entities
            return await _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        
        public async Task<Contract?> GetByContractNumberAsync(string contractNumber)
        {
            return await _context.Contracts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
        }
        
        public async Task<List<Contract>> GetAllAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .Where(c => c.IsActive);
            
            if (userId.HasValue)
                query = query.Where(c => c.UserId == userId.Value);
                
            if (groupId.HasValue)
                query = query.Where(c => c.GroupId == groupId.Value);
                
            if (startDate.HasValue)
                query = query.Where(c => c.SaleStartDate >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(c => c.SaleStartDate <= endDate.Value);
            
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Contract>> GetByUserIdAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .Where(c => c.UserId == userId && c.IsActive);
            
            if (startDate.HasValue)
                query = query.Where(c => c.SaleStartDate >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(c => c.SaleStartDate <= endDate.Value);
            
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Contract>> GetByUploadIdAsync(string uploadId)
        {
            return await _context.Contracts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .Where(c => c.UploadId == uploadId)
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
        
        public async Task<List<Contract>> CreateBatchAsync(List<Contract> contracts)
        {
            if (contracts == null || !contracts.Any())
                return new List<Contract>();
            
            // ✅ Batch insert - single transaction, single SaveChanges
            _context.Contracts.AddRange(contracts);
            await _context.SaveChangesAsync();
            
            // ✅ Reload with navigation properties for API responses
            var contractIds = contracts.Select(c => c.Id).ToList();
            var reloadedContracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Group)
                .Include(c => c.UserMatricula)
                .Where(c => contractIds.Contains(c.Id))
                .ToListAsync();
            
            return reloadedContracts;
        }
        
        public async Task<List<MonthlyProduction>> GetMonthlyProductionAsync(
            Guid? userId, 
            DateTime? startDate, 
            DateTime? endDate)
        {
            // ✅ Push grouping to database instead of loading all contracts into memory
            var query = _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive);
            
            if (userId.HasValue)
                query = query.Where(c => c.UserId == userId.Value);
            
            if (startDate.HasValue)
                query = query.Where(c => c.SaleStartDate >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(c => c.SaleStartDate <= endDate.Value);
            
            return await query
                .GroupBy(c => new { c.SaleStartDate.Year, c.SaleStartDate.Month })
                .Select(g => new MonthlyProduction
                {
                    // ✅ Use string concatenation instead of string.Format for SQL translation
                    Period = g.Key.Year.ToString() + "-" + (g.Key.Month < 10 ? "0" : "") + g.Key.Month.ToString(),
                    TotalProduction = g.Sum(c => c.TotalAmount),
                    ContractCount = g.Count()
                })
                .OrderBy(m => m.Period)
                .ToListAsync();
        }
        
        public async Task<Contract> UpdateAsync(Contract contract)
        {
            contract.UpdatedAt = DateTime.UtcNow;
            
            // ✅ Clear ALL tracked entities to avoid conflicts
            _context.ChangeTracker.Clear();
            
            // ✅ Null out navigation properties to prevent EF Core from tracking them
            contract.User = null;
            contract.Group = null;
            contract.UserMatricula = null;
            
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(contract.Id) ?? contract;
        }
    }
}