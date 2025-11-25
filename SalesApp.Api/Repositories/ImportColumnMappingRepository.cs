using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ImportColumnMappingRepository : IImportColumnMappingRepository
    {
        private readonly AppDbContext _context;

        public ImportColumnMappingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ImportColumnMapping> CreateAsync(ImportColumnMapping mapping)
        {
            _context.ImportColumnMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        }

        public async Task<ImportColumnMapping?> GetByMappingNameAsync(string name)
        {
            return await _context.ImportColumnMappings
                .Include(m => m.CreatedBy)
                .FirstOrDefaultAsync(m => m.MappingName == name);
        }

        public async Task<List<ImportColumnMapping>> GetByUserIdAsync(Guid userId)
        {
            return await _context.ImportColumnMappings
                .Include(m => m.CreatedBy)
                .Where(m => m.CreatedByUserId == userId)
                .OrderBy(m => m.MappingName)
                .ToListAsync();
        }

        public async Task<List<ImportColumnMapping>> GetAllAsync()
        {
            return await _context.ImportColumnMappings
                .Include(m => m.CreatedBy)
                .OrderBy(m => m.MappingName)
                .ToListAsync();
        }
    }
}
