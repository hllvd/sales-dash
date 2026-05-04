using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class PendingContractClaimRepository : IPendingContractClaimRepository
    {
        private readonly AppDbContext _context;

        public PendingContractClaimRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PendingContractClaim> CreateAsync(PendingContractClaim claim)
        {
            _context.PendingContractClaims.Add(claim);
            await _context.SaveChangesAsync();
            return claim;
        }

        public async Task<PendingContractClaim?> GetByContractNumberAsync(string contractNumber)
        {
            return await _context.PendingContractClaims
                .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
        }

        public async Task<PendingContractClaim?> GetByIdAsync(int id)
        {
            return await _context.PendingContractClaims.FindAsync(id);
        }

        public async Task<List<PendingContractClaim>> GetUnresolvedByContractNumbersAsync(List<string> contractNumbers)
        {
            return await _context.PendingContractClaims
                .Where(c => !c.IsResolved && contractNumbers.Contains(c.ContractNumber))
                .ToListAsync();
        }

        public async Task<List<PendingContractClaim>> GetUnresolvedByUserIdAsync(Guid userId)
        {
            return await _context.PendingContractClaims
                .Include(c => c.Matricula)
                .Where(c => !c.IsResolved && c.UserId == userId)
                .OrderByDescending(c => c.ClaimedAt)
                .ToListAsync();
        }

        public async Task<List<PendingContractClaim>> GetUnresolvedByMatriculaIdAsync(int matriculaId)
        {
            return await _context.PendingContractClaims
                .Include(c => c.User)
                .Include(c => c.Matricula)
                .Where(c => !c.IsResolved && c.MatriculaId == matriculaId)
                .OrderByDescending(c => c.ClaimedAt)
                .ToListAsync();
        }

        public async Task UpdateBatchAsync(List<PendingContractClaim> claims)
        {
            _context.PendingContractClaims.UpdateRange(claims);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(PendingContractClaim claim)
        {
            _context.PendingContractClaims.Remove(claim);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteByContractNumberAsync(string contractNumber)
        {
            var trimmedNumber = contractNumber.Trim();
            var claims = await _context.PendingContractClaims
                .Where(c => c.ContractNumber == trimmedNumber)
                .ToListAsync();

            if (claims.Any())
            {
                _context.PendingContractClaims.RemoveRange(claims);
                await _context.SaveChangesAsync();
            }
        }
    }
}
