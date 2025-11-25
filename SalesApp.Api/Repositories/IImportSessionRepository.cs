using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IImportSessionRepository
    {
        Task<ImportSession> CreateAsync(ImportSession session);
        Task<ImportSession?> GetByUploadIdAsync(string uploadId);
        Task<ImportSession?> GetByIdAsync(int id);
        Task<List<ImportSession>> GetByUserIdAsync(Guid userId);
        Task<List<ImportSession>> GetAllAsync();
        Task UpdateAsync(ImportSession session);
    }
}
