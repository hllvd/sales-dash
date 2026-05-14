using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class MatriculaRepository : IMatriculaRepository
    {
        private readonly AppDbContext _context;

        public MatriculaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Matricula?> GetByIdAsync(int id)
        {
            return await _context.Matriculas
                .Include(m => m.UserMatriculas)
                    .ThenInclude(um => um.User)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Matricula?> GetByMatriculaNumberAsync(string matriculaNumber)
        {
            return await _context.Matriculas
                .Include(m => m.UserMatriculas)
                    .ThenInclude(um => um.User)
                .FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber);
        }

        public async Task<IEnumerable<Matricula>> GetAllAsync()
        {
            return await _context.Matriculas
                .Include(m => m.UserMatriculas)
                    .ThenInclude(um => um.User)
                .ToListAsync();
        }

        public async Task<Matricula> CreateAsync(Matricula matricula)
        {
            _context.Matriculas.Add(matricula);
            await _context.SaveChangesAsync();
            return matricula;
        }

        public async Task<Matricula> UpdateAsync(Matricula matricula)
        {
            matricula.UpdatedAt = DateTime.UtcNow;
            _context.Entry(matricula).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return matricula;
        }

        public async Task DeleteAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Unlink Contracts (Bulk update)
                await _context.Contracts
                    .Where(c => c.MatriculaId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.MatriculaId, (int?)null));

                // 2. Remove User associations (Bulk delete)
                await _context.UserMatriculas
                    .Where(m => m.MatriculaId == id)
                    .ExecuteDeleteAsync();

                // 3. Remove associated PendingContractClaims (Bulk delete)
                await _context.PendingContractClaims
                    .Where(c => c.MatriculaId == id)
                    .ExecuteDeleteAsync();

                // 4. Unlink PVs (Bulk update)
                await _context.PVs
                    .Where(p => p.MatriculaId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.MatriculaId, (int?)null));

                // 5. Delete the Matricula itself
                await _context.Matriculas
                    .Where(m => m.Id == id)
                    .ExecuteDeleteAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[MatriculaRepository] Error deleting matricula {id}: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<Matricula>> GetByImportSessionIdAsync(int sessionId)
        {
            return await _context.Matriculas
                .Where(m => m.ImportSessionId == sessionId)
                .ToListAsync();
        }
    }
}
