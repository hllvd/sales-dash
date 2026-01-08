using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IContractRepository
    {
        Task<Contract?> GetByIdAsync(int id);
        Task<Contract?> GetByContractNumberAsync(string contractNumber);
        Task<List<Contract>> GetAllAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<Contract>> GetByUserIdAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<Contract>> GetByUploadIdAsync(string uploadId);
        Task<Contract> CreateAsync(Contract contract);
        Task<List<Contract>> CreateBatchAsync(List<Contract> contracts);
        Task<Contract> UpdateAsync(Contract contract);
    }
}