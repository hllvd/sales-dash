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
                .AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserMatricula?> GetByIdAsync(int id)
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<UserMatricula>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserMatriculas
                .AsNoTracking()
                .Include(m => m.Matricula)
                .Where(m => m.User.Id == userId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<UserMatricula>> GetActiveByUserIdAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            return await _context.UserMatriculas
                .AsNoTracking()
                .Include(m => m.Matricula)
                .Where(m => m.User.Id == userId && 
                           m.IsActive && 
                           (m.EndDate == null || m.EndDate > now))
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserMatricula?> GetByMatriculaIdAsync(int matriculaId)
        {
            return await _context.UserMatriculas
                .AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .FirstOrDefaultAsync(m => m.MatriculaId == matriculaId);
        }

        public async Task<List<UserMatricula>> GetAllByMatriculaIdAsync(int matriculaId)
        {
            return await _context.UserMatriculas
                .AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .Where(m => m.MatriculaId == matriculaId && m.IsActive)
                .OrderByDescending(m => m.IsOwner)
                .ThenBy(m => m.User.Name)
                .ToListAsync();
        }

        public async Task<UserMatricula> CreateAsync(UserMatricula matricula)
        {
            var existing = await _context.UserMatriculas
                .AnyAsync(m => m.UserInternalId == matricula.UserInternalId && m.MatriculaId == matricula.MatriculaId);
            
            if (existing)
            {
                throw new InvalidOperationException($"User already has this matricula link.");
            }

            matricula.CreatedAt = DateTime.UtcNow;
            matricula.UpdatedAt = DateTime.UtcNow;
            
            // ✅ Fix: If this matricula is being set as owner, unset others BEFORE adding this to the context
            // This prevents a temporary state where two owners exist (violating SQLite unique constraint)
            if (matricula.IsOwner)
            {
                var existingOwners = await _context.UserMatriculas
                    .Where(m => m.MatriculaId == matricula.MatriculaId && m.IsOwner && m.UserInternalId != matricula.UserInternalId)
                    .ToListAsync();
                
                foreach (var owner in existingOwners)
                {
                    owner.IsOwner = false;
                    owner.UpdatedAt = DateTime.UtcNow;
                }
                // Save the unsets first
                await _context.SaveChangesAsync();
            }
            
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();
            
            return await GetByIdAsync(matricula.Id) ?? matricula;
        }

        public async Task<UserMatricula> UpdateAsync(UserMatricula matricula)
        {
            var existing = await _context.UserMatriculas
                .AnyAsync(m => m.UserInternalId == matricula.UserInternalId && m.MatriculaId == matricula.MatriculaId && m.Id != matricula.Id);
            
            if (existing)
            {
                throw new InvalidOperationException($"User already has this matricula link.");
            }

            matricula.UpdatedAt = DateTime.UtcNow;
            
            // ✅ Fix: If this matricula is being set as owner, unset others BEFORE updating this record
            if (matricula.IsOwner)
            {
                var existingOwners = await _context.UserMatriculas
                    .Where(m => m.MatriculaId == matricula.MatriculaId && m.IsOwner && m.UserInternalId != matricula.UserInternalId)
                    .ToListAsync();
                
                foreach (var owner in existingOwners)
                {
                    owner.IsOwner = false;
                    owner.UpdatedAt = DateTime.UtcNow;
                }
                // Save the unsets first
                await _context.SaveChangesAsync();
            }
            
            var existingEntry = _context.ChangeTracker.Entries<UserMatricula>()
                .FirstOrDefault(e => e.Entity.Id == matricula.Id);
            
            if (existingEntry == null)
            {
                _context.UserMatriculas.Update(matricula);
            }
            else
            {
                _context.Entry(existingEntry.Entity).CurrentValues.SetValues(matricula);
            }
            
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
                .AnyAsync(m => m.MatriculaId == matriculaId && 
                              m.User.Id == userId && 
                              m.IsActive &&
                              (m.EndDate == null || m.EndDate > now));
        }

        public async Task<UserMatricula?> GetOwnerByMatriculaIdAsync(int matriculaId)
        {
            return await _context.UserMatriculas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MatriculaId == matriculaId && m.IsOwner);
        }

        public async Task SetOwnerAsync(int matriculaId, Guid newOwnerId)
        {
            var existingLinks = await _context.UserMatriculas
                .Include(m => m.User)
                .Where(m => m.MatriculaId == matriculaId)
                .ToListAsync();
            
            foreach (var link in existingLinks)
            {
                link.IsOwner = (link.User?.Id == newOwnerId);
                link.UpdatedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task<UserMatricula?> GetByMatriculaNumberAndUserIdAsync(string matriculaNumber, Guid userId)
        {
            return await _context.UserMatriculas
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .FirstOrDefaultAsync(m => m.Matricula.MatriculaNumber == matriculaNumber && m.User.Id == userId);
        }
    }
}
