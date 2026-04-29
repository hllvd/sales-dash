using SalesApp.DTOs;

namespace SalesApp.Services
{
    public class WizardContractImportOptions
    {
        public bool SkipMissingContractNumber { get; set; } = true;
        public bool AllowAutoCreateGroups { get; set; } = true;
        public bool AllowAutoCreatePVs { get; set; } = true;
        public string DateFormat { get; set; } = "MM/DD/YYYY";
    }

    public interface IWizardService
    {
        Task<ImportPreviewResponse> ProcessStep1UploadAsync(IFormFile file, Guid userId);
        Task<byte[]> GenerateUsersTemplateAsync(string uploadId);
        Task<ImportStatusResponse> ProcessStep2ImportAsync(string uploadId, IFormFile usersFile, Guid userId);
        /// <summary>Generates the enriched contracts XLSX and persists it to the wizard temp folder for audit.</summary>
        Task<byte[]> GenerateEnrichedContractsAsync(string uploadId, Guid userId);
        /// <summary>Reads the persisted temp file and imports it as Contracts (templateId=2).</summary>
        Task<ImportStatusResponse> ImportWizardContractsAsync(string uploadId, Guid userId, WizardContractImportOptions options);
    }
}
