using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ImportSessionRepository : IImportSessionRepository
    {
        private readonly AppDbContext _context;

        public ImportSessionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ImportSession> CreateAsync(ImportSession session)
        {
            _context.ImportSessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<ImportSession?> GetByUploadIdAsync(string uploadId)
        {
            return await _context.ImportSessions
                .Include(s => s.Template)
                .Include(s => s.UploadedBy)
                .FirstOrDefaultAsync(s => s.UploadId == uploadId);
        }

        public async Task<ImportSession?> GetByIdAsync(int id)
        {
            return await _context.ImportSessions
                .Include(s => s.Template)
                .Include(s => s.UploadedBy)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<ImportSession>> GetByUserIdAsync(Guid userId)
        {
            return await _context.ImportSessions
                .Include(s => s.Template)
                .Include(s => s.UploadedBy)
                .Where(s => s.UploadedByUserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ImportSession>> GetAllAsync()
        {
            return await _context.ImportSessions
                .Include(s => s.Template)
                .Include(s => s.UploadedBy)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(ImportSession session)
        {
            _context.ImportSessions.Update(session);
            await _context.SaveChangesAsync();
        }
    }
}
