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
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        
        public async Task<Contract?> GetByContractNumberAsync(string contractNumber)
        {
            return await _context.Contracts
                .AsNoTracking()
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
        }

        public async Task<List<Contract>> GetByContractNumbersAsync(List<string> contractNumbers)
        {
            if (contractNumbers == null || !contractNumbers.Any())
                return new List<Contract>();

            return await _context.Contracts
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .Where(c => contractNumbers.Contains(c.ContractNumber))
                .ToListAsync();
        }
        
        public async Task<List<Contract>> GetAllAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, string? matriculaNumber = null, string? userEmail = null, UserScopeContext? scope = null)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .Where(c => c.IsActive);

            // Apply hierarchical data scope before any other filters
            if (scope != null && !scope.IsGlobal)
            {
                // Sargable IN clauses for efficient filtering
                query = query.Where(c => 
                    (c.User != null && scope.AllowedUserIds.Contains(c.User.Id)) ||
                    (!string.IsNullOrEmpty(c.TempMatricula) && scope.AllowedMatriculas.Contains(c.TempMatricula)) ||
                    (c.Matricula != null && scope.AllowedMatriculas.Contains(c.Matricula.MatriculaNumber))
                );
            }
            
            if (userId.HasValue)
                query = query.Where(c => c.User.Id == userId.Value);
                
            if (!string.IsNullOrEmpty(userEmail))
            {
                var normalizedEmail = userEmail.Trim().ToLower();
                query = query.Where(c => c.User != null && c.User.Email.ToLower() == normalizedEmail);
            }
            
            if (showUnassigned.HasValue)
            {
                if (showUnassigned.Value)
                    query = query.Where(c => c.UserInternalId == null);
                else
                    query = query.Where(c => c.UserInternalId != null);
            }
                
            if (groupId.HasValue)
                query = query.Where(c => c.GroupId == groupId.Value);
                
            if (startDate.HasValue)
                query = query.Where(c => c.SaleStartDate >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(c => c.SaleStartDate <= endDate.Value);
 
            if (!string.IsNullOrEmpty(contractNumber))
                query = query.Where(c => c.ContractNumber == contractNumber);

            if (!string.IsNullOrWhiteSpace(matriculaNumber))
            {
                var normalizedMatricula = matriculaNumber.Trim().ToLower();
                query = query.Where(c => 
                    (c.Matricula != null && c.Matricula.MatriculaNumber.ToLower().Contains(normalizedMatricula)) ||
                    (!string.IsNullOrEmpty(c.TempMatricula) && c.TempMatricula.ToLower().Contains(normalizedMatricula))
                );
            }
            
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Contract>> GetByUserIdAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, string? matriculaNumber = null)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .Where(c => c.User.Id == userId && c.IsActive);
            
            if (startDate.HasValue)
                query = query.Where(c => c.SaleStartDate >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(c => c.SaleStartDate <= endDate.Value);

            if (!string.IsNullOrWhiteSpace(matriculaNumber))
            {
                var normalizedMatricula = matriculaNumber.Trim().ToLower();
                query = query.Where(c => 
                    (c.Matricula != null && c.Matricula.MatriculaNumber.ToLower().Contains(normalizedMatricula)) ||
                    (!string.IsNullOrEmpty(c.TempMatricula) && c.TempMatricula.ToLower().Contains(normalizedMatricula))
                );
            }
            
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }
        
        public async Task<List<Contract>> GetByUploadIdAsync(string uploadId)
        {
            return await _context.Contracts
                .AsNoTracking()
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .Where(c => c.UploadId == uploadId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<Contract> CreateAsync(Contract contract)
        {
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();
            
            // Explicitly load ContractStatus to ensure it's available for MapToContractResponse
            await _context.Entry(contract).Reference(c => c.ContractStatus).LoadAsync();
            
            // Reload with other navigation properties
            return await GetByIdAsync(contract.Id) ?? contract;
        }
        
        public async Task<List<Contract>> CreateBatchAsync(List<Contract> contracts)
        {
            if (contracts == null || !contracts.Any())
                return new List<Contract>();
            
            try
            {
                // ✅ Batch insert - single transaction, single SaveChanges
                _context.Contracts.AddRange(contracts);
                await _context.SaveChangesAsync();
                
                // ✅ Reload with navigation properties for API responses
                var contractIds = contracts.Select(c => c.Id).ToList();
                var reloadedContracts = await _context.Contracts
                    .AsNoTracking()
                    .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                    .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                    .Include(c => c.Group)
                    .Include(c => c.PV)
                    .Include(c => c.ContractStatus)
                    .Where(c => contractIds.Contains(c.Id))
                    .ToListAsync();
                
                return reloadedContracts;
            }
            catch (Exception)
            {
                // ✅ CRITICAL: Clear change tracker to avoid invalid entities lingering in the context
                // This prevents subsequent SaveChangesAsync calls (like updating session status) from failing
                _context.ChangeTracker.Clear();
                throw;
            }
        }
        
        public async Task<List<MonthlyProduction>> GetMonthlyProductionAsync(
            Guid? userId, 
            DateTime? startDate, 
            DateTime? endDate,
            bool? showUnassigned = null)
        {
            // ✅ Push grouping to database instead of loading all contracts into memory
            var query = _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive);
            
            if (userId.HasValue)
                query = query.Where(c => c.User.Id == userId.Value);
                
            if (showUnassigned.HasValue)
            {
                if (showUnassigned.Value)
                    query = query.Where(c => c.UserInternalId == null);
                else
                    query = query.Where(c => c.UserInternalId != null);
            }
            
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
            contract.Matricula = null;
            contract.PV = null;
            contract.ContractStatus = null;
            
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
            
            // Explicitly load ContractStatus
            await _context.Entry(contract).Reference(c => c.ContractStatus).LoadAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(contract.Id) ?? contract;
        }

        public async Task<List<MatriculaHealthResponse>> GetMatriculaHealthAsync()
        {
            var now = DateTime.UtcNow;
            
            // ✅ Optimized query: group by matricula and find the latest update
            // We project to an anonymous type first to ensure EF Core translates the null coalescing correctly
            var healthData = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new 
                { 
                    Matricula = c.Matricula != null ? c.Matricula.MatriculaNumber : c.TempMatricula,
                    c.UpdatedAt 
                })
                .Where(x => !string.IsNullOrEmpty(x.Matricula))
                .GroupBy(x => x.Matricula)
                .Select(g => new
                {
                    Matricula = g.Key!,
                    LastUpdate = g.Max(x => x.UpdatedAt),
                    Count = g.Count()
                })
                .OrderBy(h => h.LastUpdate)
                .ToListAsync();

            return healthData.Select(h => new MatriculaHealthResponse
            {
                Matricula = h.Matricula,
                LastUpdate = DateTime.SpecifyKind(h.LastUpdate, DateTimeKind.Utc),
                ContractCount = h.Count,
                Status = (now - h.LastUpdate).TotalHours switch
                {
                    > 168 => "Danger",
                    > 72 => "OutOfDate",
                    > 36 => "Warning",
                    _ => "Healthy"
                }
            }).ToList();
        }
    }
}