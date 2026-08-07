using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class StoreRepository : IStoreRepository
    {
        private readonly AppDbContext _context;

        public StoreRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Store?> GetByIdAsync(int id)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Store?> GetByNameAsync(string name)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(s => s.Name.ToLower() == name.Trim().ToLower());
        }

        public async Task<IEnumerable<Store>> GetAllAsync()
        {
            return await _context.Stores
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Store>> GetActiveAsync()
        {
            return await _context.Stores
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Store> CreateAsync(Store store)
        {
            store.CreatedAt = DateTime.UtcNow;
            store.UpdatedAt = DateTime.UtcNow;
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();
            return store;
        }

        public async Task<Store> UpdateAsync(Store store)
        {
            store.UpdatedAt = DateTime.UtcNow;
            _context.Stores.Update(store);
            await _context.SaveChangesAsync();
            return store;
        }

        public async Task DeleteAsync(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store != null)
            {
                var teams = await _context.Teams.Where(t => t.StoreId == id).ToListAsync();
                foreach (var team in teams)
                {
                    team.StoreId = null;
                }
                _context.Stores.Remove(store);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            var normalized = name.Trim().ToLower();
            if (excludeId.HasValue)
            {
                return await _context.Stores.AnyAsync(s => s.Id != excludeId.Value && s.Name.ToLower() == normalized);
            }
            return await _context.Stores.AnyAsync(s => s.Name.ToLower() == normalized);
        }
    }
}
