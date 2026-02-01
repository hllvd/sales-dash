using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IContractMetadataRepository
    {
        Task<ContractMetadata?> GetByNameAndValueAsync(string name, string value);
        Task<ContractMetadata> CreateAsync(ContractMetadata metadata);
    }
}
