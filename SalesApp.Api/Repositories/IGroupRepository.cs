using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IGroupRepository
    {
        Task<Group?> GetByIdAsync(int id);
        Task<List<Group>> GetAllAsync();
        Task<Group> CreateAsync(Group group);
        Task<Group> UpdateAsync(Group group);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
    }
}