using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IImportUserMappingRepository
    {
        Task<ImportUserMapping> CreateAsync(ImportUserMapping mapping);
        Task<List<ImportUserMapping>> GetByImportSessionIdAsync(int sessionId);
        Task UpdateAsync(ImportUserMapping mapping);
        Task<ImportUserMapping?> GetByIdAsync(int id);
    }
}
