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
                .FirstOrDefaultAsync(c => c.Id == id && c.ContractStatus.Name.ToLower() != "desistente");
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
        
        private IQueryable<Contract> BuildFilteredQuery(
            Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null,
            string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null,
            string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null,
            List<string>? statuses = null, bool isSuperAdmin = false)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive);

            var allowsDesistente = isSuperAdmin && statuses != null && statuses.Any(s => s.Equals("Desistente", StringComparison.OrdinalIgnoreCase));

            if (!allowsDesistente)
            {
                query = query.Where(c => c.ContractStatus.Name.ToLower() != "desistente");
            }

            if (statuses != null && statuses.Count > 0)
            {
                var lowerStatuses = statuses.Select(s => s.ToLower()).ToList();
                query = query.Where(c => lowerStatuses.Contains(c.ContractStatus.Name.ToLower()));
            }

            // Apply hierarchical data scope before any other filters
            if (scope != null && !scope.IsGlobal)
            {
                // Sargable IN clauses for efficient filtering
                query = query.Where(c => 
                    (c.User != null && scope.AllowedUserIds.Contains(c.User.Id)) ||
                    (!string.IsNullOrEmpty(c.TempMatricula) && scope.AllowedMatriculas.Contains(c.TempMatricula)) ||
                    (c.Matricula != null && scope.AllowedMatriculas.Contains(c.Matricula.MatriculaNumber)) ||
                    (scope.AdminLinkedMatriculas.Count > 0 && (
                        (!string.IsNullOrEmpty(c.TempMatricula) && scope.AdminLinkedMatriculas.Contains(c.TempMatricula)) ||
                        (c.Matricula != null && scope.AdminLinkedMatriculas.Contains(c.Matricula.MatriculaNumber))
                    ) && (
                        c.UserInternalId == null ||
                        (c.User != null && scope.AllowedUserIds.Contains(c.User.Id))
                    ))
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

            if (matriculaNumbers != null && matriculaNumbers.Count > 0)
            {
                var normalizedMatriculas = matriculaNumbers
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim().ToLower())
                    .ToList();

                if (normalizedMatriculas.Count > 0)
                {
                    query = query.Where(c => 
                        (c.Matricula != null && normalizedMatriculas.Any(n => c.Matricula.MatriculaNumber.ToLower().Contains(n))) ||
                        (!string.IsNullOrEmpty(c.TempMatricula) && normalizedMatriculas.Any(n => c.TempMatricula.ToLower().Contains(n)))
                    );
                }
            }

            // Filter by team membership (point-in-time): contract sale date must fall within member's team tenure
            if (teamIds != null && teamIds.Count > 0)
            {
                query = query.Where(c => c.UserInternalId != null && _context.UserTeams.Any(ut =>
                    teamIds.Contains(ut.TeamId) &&
                    ut.UserInternalId == c.UserInternalId.Value &&
                    c.SaleStartDate >= ut.StartDate &&
                    (ut.EndDate == null || c.SaleStartDate <= ut.EndDate)
                ));
            }

            if (userIds != null && userIds.Count > 0)
            {
                query = query.Where(c => c.User != null && userIds.Contains(c.User.Id));
            }

            return query;
        }

        public async Task<List<Contract>> GetAllAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false)
        {
            var query = BuildFilteredQuery(userId, groupId, startDate, endDate, contractNumber, showUnassigned, matriculaNumbers, userEmail, scope, teamIds, userIds, statuses, isSuperAdmin);
            
            return await query
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<Contract> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false)
        {
            var query = BuildFilteredQuery(userId, groupId, startDate, endDate, contractNumber, showUnassigned, matriculaNumbers, userEmail, scope, teamIds, userIds, statuses, isSuperAdmin);

            int totalCount = await query.CountAsync();

            var items = await query
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .OrderByDescending(c => c.SaleStartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<ContractAggregation> GetAggregationAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false)
        {
            var query = BuildFilteredQuery(userId, groupId, startDate, endDate, contractNumber, showUnassigned, matriculaNumbers, userEmail, scope, teamIds, userIds, statuses, isSuperAdmin);

            var groupings = await query
                .GroupBy(c => c.ContractStatus.Name)
                .Select(g => new
                {
                    StatusName = g.Key,
                    TotalAmount = g.Sum(c => c.TotalAmount)
                })
                .ToListAsync();

            decimal total = 0m;
            decimal totalCancel = 0m;
            decimal totalActive = 0m;
            decimal totalLate = 0m;

            var defaultedName = ContractStatus.Defaulted.ToApiString();
            var late1Name = ContractStatus.Late1.ToApiString();
            var late2Name = ContractStatus.Late2.ToApiString();
            var late3Name = ContractStatus.Late3.ToApiString();
            var awaitingPaymentName = ContractStatus.AwaitingPayment.ToApiString();

            foreach (var g in groupings)
            {
                var amount = g.TotalAmount;
                var status = g.StatusName;

                if (status.Equals(awaitingPaymentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                total += amount;

                if (status.Equals(defaultedName, StringComparison.OrdinalIgnoreCase))
                {
                    totalCancel += amount;
                }
                else
                {
                    totalActive += amount;

                    if (status.Equals(late1Name, StringComparison.OrdinalIgnoreCase) ||
                        status.Equals(late2Name, StringComparison.OrdinalIgnoreCase) ||
                        status.Equals(late3Name, StringComparison.OrdinalIgnoreCase))
                    {
                        totalLate += amount;
                    }
                }
            }

            var retention = total > 0 ? totalActive / total : 0m;
            var strictRetention = total > 0 ? (totalActive - totalLate) / total : 0m;

            return new ContractAggregation
            {
                Total = total,
                TotalCancel = totalCancel,
                TotalActive = totalActive,
                TotalLate = totalLate,
                Retention = retention,
                StrictRetention = strictRetention
            };
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
                .Where(c => c.User.Id == userId && c.IsActive && c.ContractStatus.Name.ToLower() != "desistente");
            
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
                .Where(c => c.UploadId == uploadId && c.ContractStatus.Name.ToLower() != "desistente")
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<Contract> CreateAsync(Contract contract)
        {
            contract.ContractNumber = Utils.NormalizationUtils.NormalizeNumber(contract.ContractNumber);
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
                foreach (var contract in contracts)
                {
                    contract.ContractNumber = Utils.NormalizationUtils.NormalizeNumber(contract.ContractNumber);
                }

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
                .Where(c => c.IsActive && c.ContractStatus.Name.ToLower() != "desistente");
            
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

        public async Task<List<Contract>> GetContractsForMigrationAsync(Guid userId)
        {
            return await _context.Contracts
                .Include(c => c.User)
                .Include(c => c.Matricula)
                .Include(c => c.ContractStatus)
                .Where(c => c.User.Id == userId)
                .ToListAsync();
        }
    }
}