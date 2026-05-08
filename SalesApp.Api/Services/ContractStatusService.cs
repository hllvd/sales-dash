using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SalesApp.Services
{
    public class ContractStatusService : IContractStatusService
    {
        private readonly AppDbContext _context;
        private static readonly ConcurrentDictionary<string, int> _cache = new();

        public ContractStatusService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetStatusIdByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Status name cannot be empty.");

            // Standardize name (case-insensitive mapping)
            var standardizedName = name.Trim();

            // Return from cache if available
            if (_cache.TryGetValue(standardizedName, out var cachedId))
                return cachedId;

            // Look up in DB
            var status = await _context.ContractStatuses
                .FirstOrDefaultAsync(s => s.Name == standardizedName);

            if (status == null)
            {
                // Auto-create if it doesn't exist (safety for new statuses found in imports)
                status = new ContractStatusEntity { Name = standardizedName };
                _context.ContractStatuses.Add(status);
                await _context.SaveChangesAsync();
            }

            _cache[standardizedName] = status.Id;
            return status.Id;
        }

        public async Task<List<ContractStatusEntity>> GetAllStatusesAsync()
        {
            return await _context.ContractStatuses.ToListAsync();
        }

        public async Task EnsureStatusesExistAsync(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                await GetStatusIdByNameAsync(name);
            }
        }
    }
}
