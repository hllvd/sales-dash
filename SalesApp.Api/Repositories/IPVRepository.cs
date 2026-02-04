using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IPVRepository
    {
        Task<List<PV>> GetAllAsync();
        Task<PV?> GetByIdAsync(int id);
        Task<PV?> GetByNameAsync(string name);
        Task<PV> CreateAsync(PV pv);
        Task<PV> UpdateAsync(PV pv);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}
