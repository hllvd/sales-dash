using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ImportTemplateRepository : IImportTemplateRepository
    {
        private readonly AppDbContext _context;

        public ImportTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ImportTemplate> CreateAsync(ImportTemplate template)
        {
            _context.ImportTemplates.Add(template);
            await _context.SaveChangesAsync();
            return template;
        }

        public async Task<ImportTemplate?> GetByIdAsync(int id)
        {
            return await _context.ImportTemplates
                .Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        }

        public async Task<ImportTemplate?> GetByNameAsync(string name)
        {
            return await _context.ImportTemplates
                .Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Name == name && t.IsActive);
        }

        public async Task<List<ImportTemplate>> GetAllAsync()
        {
            return await _context.ImportTemplates
                .Include(t => t.CreatedBy)
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<ImportTemplate>> GetByEntityTypeAsync(string entityType)
        {
            return await _context.ImportTemplates
                .Include(t => t.CreatedBy)
                .Where(t => t.EntityType == entityType && t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task UpdateAsync(ImportTemplate template)
        {
            template.UpdatedAt = DateTime.UtcNow;
            _context.ImportTemplates.Update(template);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var template = await _context.ImportTemplates.FindAsync(id);
            if (template != null)
            {
                template.IsActive = false;
                template.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
