using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IImportTemplateRepository
    {
        Task<ImportTemplate> CreateAsync(ImportTemplate template);
        Task<ImportTemplate?> GetByIdAsync(int id);
        Task<ImportTemplate?> GetByNameAsync(string name);
        Task<List<ImportTemplate>> GetAllAsync();
        Task<List<ImportTemplate>> GetByEntityTypeAsync(string entityType);
        Task UpdateAsync(ImportTemplate template);
        Task DeleteAsync(int id);
    }
}
