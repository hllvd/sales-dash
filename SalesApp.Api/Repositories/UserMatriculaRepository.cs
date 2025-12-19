using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class UserMatriculaRepository : IUserMatriculaRepository
    {
        private readonly AppDbContext _context;

        public UserMatriculaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserMatricula>> GetAllAsync()
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserMatricula?> GetByIdAsync(int id)
        {
            return await _context.UserMatriculas
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<UserMatricula>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserMatriculas
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.StartDate)
                .ToListAsync();
        }

        public async Task<List<UserMatricula>> GetActiveByUserIdAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            return await _context.UserMatriculas
                .Where(m => m.UserId == userId && 
                           m.IsActive && 
                           (m.EndDate == null || m.EndDate > now))
                .OrderByDescending(m => m.StartDate)
                .ToListAsync();
        }

        public async Task<UserMatricula?> GetByMatriculaNumberAsync(string matriculaNumber)
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber);
        }

        public async Task<List<UserMatricula>> GetAllByMatriculaNumberAsync(string matriculaNumber)
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .Where(m => m.MatriculaNumber == matriculaNumber)
                .OrderByDescending(m => m.IsOwner)
                .ThenBy(m => m.User.Name)
                .ToListAsync();
        }

        public async Task<UserMatricula> CreateAsync(UserMatricula matricula)
        {
            matricula.CreatedAt = DateTime.UtcNow;
            matricula.UpdatedAt = DateTime.UtcNow;
            
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();
            
            // If this matricula is being set as owner, remove owner flag from others
            if (matricula.IsOwner)
            {
                await SetOwnerAsync(matricula.MatriculaNumber, matricula.UserId);
            }
            
            return await GetByIdAsync(matricula.Id) ?? matricula;
        }

        public async Task<UserMatricula> UpdateAsync(UserMatricula matricula)
        {
            matricula.UpdatedAt = DateTime.UtcNow;
            
            // If this matricula is being set as owner, remove owner flag from others
            if (matricula.IsOwner)
            {
                await SetOwnerAsync(matricula.MatriculaNumber, matricula.UserId);
            }
            
            _context.UserMatriculas.Update(matricula);
            await _context.SaveChangesAsync();
            
            return matricula;
        }

        public async Task DeleteAsync(int id)
        {
            var matricula = await _context.UserMatriculas.FindAsync(id);
            if (matricula != null)
            {
                _context.UserMatriculas.Remove(matricula);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsMatriculaValidForUser(Guid userId, int matriculaId)
        {
            var now = DateTime.UtcNow;
            return await _context.UserMatriculas
                .AnyAsync(m => m.Id == matriculaId && 
                              m.UserId == userId && 
                              m.IsActive &&
                              (m.EndDate == null || m.EndDate > now));
        }

        public async Task<bool> MatriculaExistsAsync(string matriculaNumber)
        {
            return await _context.UserMatriculas
                .AnyAsync(m => m.MatriculaNumber == matriculaNumber);
        }

        public async Task<UserMatricula?> GetOwnerByMatriculaNumberAsync(string matriculaNumber)
        {
            return await _context.UserMatriculas
                .FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber && m.IsOwner);
        }

        public async Task SetOwnerAsync(string matriculaNumber, Guid newOwnerId)
        {
            // Get all matriculas with this number
            var existingMatriculas = await _context.UserMatriculas
                .Where(m => m.MatriculaNumber == matriculaNumber)
                .ToListAsync();
            
            // Set IsOwner based on whether it belongs to the new owner
            foreach (var matricula in existingMatriculas)
            {
                matricula.IsOwner = (matricula.UserId == newOwnerId);
                matricula.UpdatedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
        }
        public async Task<UserMatricula?> GetByMatriculaNumberAndUserIdAsync(string matriculaNumber, Guid userId)
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber && m.UserId == userId);
        }
    }
}
