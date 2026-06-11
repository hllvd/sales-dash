using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IUserMetadataRepository
    {
        // Field definition CRUD
        Task<List<UserMetadataField>> GetActiveFieldsAsync();
        Task<List<UserMetadataField>> GetAllFieldsAsync();
        Task<UserMetadataField?> GetFieldByIdAsync(int id);
        Task<UserMetadataField?> GetFieldByKeyAsync(string key);
        Task<UserMetadataField> CreateFieldAsync(UserMetadataField field);
        Task<UserMetadataField> UpdateFieldAsync(UserMetadataField field);
        Task<bool> DeleteFieldAsync(int id); // Soft delete

        // User values
        Task<List<UserMetadataValue>> GetValuesByUserInternalIdAsync(int userInternalId);
        Task UpsertValuesAsync(int userInternalId, IList<UserMetadataValueItem> items);
    }
}
