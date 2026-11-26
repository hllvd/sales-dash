using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IFileHashCacheService
    {
        Task<FileHashMetadata?> GetHashMetadataAsync(string fileHash);
        Task<Dictionary<string, FileHashCheckResultDto>> CheckHashesAsync(IEnumerable<string> fileHashes);
        Task SaveHashMetadataAsync(FileHashMetadata metadata);
    }
}
