using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(Guid id);
        Task<List<Sale>> GetAllAsync(Guid? userId = null, Guid? groupId = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<Sale>> GetByUserIdAsync(Guid userId);
        Task<Sale> CreateAsync(Sale sale);
        Task<Sale> UpdateAsync(Sale sale);
    }
}