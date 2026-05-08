using SalesApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SalesApp.Services
{
    public interface IContractStatusService
    {
        Task<int> GetStatusIdByNameAsync(string name);
        Task<List<ContractStatusEntity>> GetAllStatusesAsync();
        Task EnsureStatusesExistAsync(IEnumerable<string> names);
    }
}
