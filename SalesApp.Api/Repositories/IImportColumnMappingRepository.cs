using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IImportColumnMappingRepository
    {
        Task<ImportColumnMapping> CreateAsync(ImportColumnMapping mapping);
        Task<ImportColumnMapping?> GetByMappingNameAsync(string name);
        Task<List<ImportColumnMapping>> GetByUserIdAsync(Guid userId);
        Task<List<ImportColumnMapping>> GetAllAsync();
    }
}
