using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class UserMetadataRepository : IUserMetadataRepository
    {
        private readonly AppDbContext _context;

        public UserMetadataRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserMetadataField>> GetActiveFieldsAsync()
        {
            return await _context.UserMetadataFields
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.Label)
                .ToListAsync();
        }

        public async Task<List<UserMetadataField>> GetAllFieldsAsync()
        {
            return await _context.UserMetadataFields
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.Label)
                .ToListAsync();
        }

        public async Task<UserMetadataField?> GetFieldByIdAsync(int id)
        {
            return await _context.UserMetadataFields
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<UserMetadataField?> GetFieldByKeyAsync(string key)
        {
            return await _context.UserMetadataFields
                .FirstOrDefaultAsync(f => f.Key == key);
        }

        public async Task<UserMetadataField> CreateFieldAsync(UserMetadataField field)
        {
            field.CreatedAt = DateTime.UtcNow;
            _context.UserMetadataFields.Add(field);
            await _context.SaveChangesAsync();
            return field;
        }

        public async Task<UserMetadataField> UpdateFieldAsync(UserMetadataField field)
        {
            _context.Entry(field).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return field;
        }

        public async Task<bool> DeleteFieldAsync(int id)
        {
            var field = await GetFieldByIdAsync(id);
            if (field == null) return false;

            field.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserMetadataValue>> GetValuesByUserInternalIdAsync(int userInternalId)
        {
            return await _context.UserMetadataValues
                .Include(v => v.Field)
                .Where(v => v.UserInternalId == userInternalId && v.Field.IsActive)
                .ToListAsync();
        }

        public async Task UpsertValuesAsync(int userInternalId, IList<UserMetadataValueItem> items)
        {
            foreach (var item in items)
            {
                var existing = await _context.UserMetadataValues
                    .FirstOrDefaultAsync(v => v.UserInternalId == userInternalId && v.UserMetadataFieldId == item.FieldId);

                if (existing != null)
                {
                    if (string.IsNullOrWhiteSpace(item.Value))
                    {
                        _context.UserMetadataValues.Remove(existing);
                    }
                    else
                    {
                        existing.Value = item.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _context.UserMetadataValues.Update(existing);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    var newValue = new UserMetadataValue
                    {
                        UserInternalId = userInternalId,
                        UserMetadataFieldId = item.FieldId,
                        Value = item.Value,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserMetadataValues.Add(newValue);
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}
