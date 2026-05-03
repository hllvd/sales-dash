using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IPendingContractClaimRepository
    {
        Task<PendingContractClaim> CreateAsync(PendingContractClaim claim);
        Task<PendingContractClaim?> GetByContractNumberAsync(string contractNumber);
        Task<List<PendingContractClaim>> GetUnresolvedByContractNumbersAsync(List<string> contractNumbers);
        Task<List<PendingContractClaim>> GetUnresolvedByUserIdAsync(Guid userId);
        Task<List<PendingContractClaim>> GetUnresolvedByMatriculaIdAsync(int matriculaId);
        Task UpdateBatchAsync(List<PendingContractClaim> claims);
    }
}
