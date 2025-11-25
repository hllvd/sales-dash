using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ImportUserMappingRepository : IImportUserMappingRepository
    {
        private readonly AppDbContext _context;

        public ImportUserMappingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ImportUserMapping> CreateAsync(ImportUserMapping mapping)
        {
            _context.ImportUserMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        }

        public async Task<List<ImportUserMapping>> GetByImportSessionIdAsync(int sessionId)
        {
            return await _context.ImportUserMappings
                .Include(m => m.ResolvedUser)
                .Include(m => m.ImportSession)
                .Where(m => m.ImportSessionId == sessionId)
                .ToListAsync();
        }

        public async Task UpdateAsync(ImportUserMapping mapping)
        {
            _context.ImportUserMappings.Update(mapping);
            await _context.SaveChangesAsync();
        }

        public async Task<ImportUserMapping?> GetByIdAsync(int id)
        {
            return await _context.ImportUserMappings
                .Include(m => m.ResolvedUser)
                .Include(m => m.ImportSession)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
    }
}
