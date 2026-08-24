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
                .Where(s => s.UploadedBy.Id == userId)
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
            var trackedSession = await _context.ImportSessions.FindAsync(session.Id);
            if (trackedSession != null)
            {
                trackedSession.Status = session.Status;
                trackedSession.ProcessedRows = session.ProcessedRows;
                trackedSession.FailedRows = session.FailedRows;
                trackedSession.CompletedAt = session.CompletedAt;
                trackedSession.Mappings = session.Mappings;
                trackedSession.TotalRows = session.TotalRows;
            }
            else
            {
                _context.ImportSessions.Attach(session);
                _context.Entry(session).Property(s => s.Status).IsModified = true;
                _context.Entry(session).Property(s => s.ProcessedRows).IsModified = true;
                _context.Entry(session).Property(s => s.FailedRows).IsModified = true;
                _context.Entry(session).Property(s => s.CompletedAt).IsModified = true;
                _context.Entry(session).Property(s => s.Mappings).IsModified = true;
                _context.Entry(session).Property(s => s.TotalRows).IsModified = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
