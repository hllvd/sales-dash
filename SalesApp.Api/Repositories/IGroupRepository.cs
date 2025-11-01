using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IGroupRepository
    {
        Task<Group?> GetByIdAsync(Guid id);
        Task<List<Group>> GetAllAsync();
        Task<Group> CreateAsync(Group group);
        Task<Group> UpdateAsync(Group group);
        Task<bool> NameExistsAsync(string name, Guid? excludeId = null);
    }
}