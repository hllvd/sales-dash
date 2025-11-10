using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface ISaleRepository
    {
        Task<Contract?> GetByIdAsync(Guid id);
        Task<List<Contract>> GetAllAsync(Guid? userId = null, Guid? groupId = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<Contract>> GetByUserIdAsync(Guid userId);
        Task<Contract> CreateAsync(Contract sale);
        Task<Contract> UpdateAsync(Contract sale);
    }
}