using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class ContractMetadataRepository : IContractMetadataRepository
    {
        private readonly AppDbContext _context;

        public ContractMetadataRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContractMetadata?> GetByNameAndValueAsync(string name, string value)
        {
            return await _context.ContractMetadata
                .FirstOrDefaultAsync(m => m.Name == name && m.Value == value);
        }

        public async Task<ContractMetadata> CreateAsync(ContractMetadata metadata)
        {
            _context.ContractMetadata.Add(metadata);
            await _context.SaveChangesAsync();
            return metadata;
        }
    }
}
