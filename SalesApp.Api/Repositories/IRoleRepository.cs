using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IRoleRepository
    {
        Task<Role?> GetByIdAsync(int id);
        Task<Role?> GetByNameAsync(string name);
        Task<List<Role>> GetAllAsync();
        Task<Role> CreateAsync(Role role);
        Task<Role> UpdateAsync(Role role);
        Task DeleteAsync(int id);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
    }
}