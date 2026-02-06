using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IWizardService
    {
        Task<ImportPreviewResponse> ProcessStep1UploadAsync(IFormFile file, Guid userId);
        Task<byte[]> GenerateUsersTemplateAsync(string uploadId);
        Task<ImportStatusResponse> ProcessStep2ImportAsync(string uploadId, IFormFile usersFile, Guid userId);
        Task<byte[]> GenerateEnrichedContractsAsync(string uploadId);
    }
}
