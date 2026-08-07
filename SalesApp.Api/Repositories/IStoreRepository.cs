using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IStoreRepository
    {
        Task<Store?> GetByIdAsync(int id);
        Task<Store?> GetByNameAsync(string name);
        Task<IEnumerable<Store>> GetAllAsync();
        Task<IEnumerable<Store>> GetActiveAsync();
        Task<Store> CreateAsync(Store store);
        Task<Store> UpdateAsync(Store store);
        Task DeleteAsync(int id);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
    }
}
