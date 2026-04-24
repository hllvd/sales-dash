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
            var matricula = await _context.Matriculas.FindAsync(id);
            if (matricula != null)
            {
                _context.Matriculas.Remove(matricula);
                await _context.SaveChangesAsync();
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
