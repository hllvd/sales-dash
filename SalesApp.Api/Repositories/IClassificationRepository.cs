using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IClassificationLevelRepository
    {
        Task<ClassificationLevel?> GetByIdAsync(int id);
        Task<List<ClassificationLevel>> GetAllAsync();
        Task<ClassificationLevel> CreateAsync(ClassificationLevel level);
        Task<ClassificationLevel> UpdateAsync(ClassificationLevel level);
        Task DeleteAsync(int id);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
        Task<int> GetActiveUsersCountAsync(int levelId);
    }

    public interface IUserClassificationRepository
    {
        Task<List<UserClassification>> GetForUserAsync(int userInternalId);
        Task<UserClassification?> GetActiveForUserAsync(int userInternalId);
        Task<List<UserClassification>> GetForLevelAsync(int levelId);
        Task<UserClassification> CreateAsync(UserClassification classification);
        Task<UserClassification> UpdateAsync(UserClassification classification);
        Task<UserClassification?> GetByIdAsync(int id);
    }
}
