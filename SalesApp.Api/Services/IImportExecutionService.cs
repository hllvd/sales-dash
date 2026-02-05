using SalesApp.Models;

namespace SalesApp.Services
{
    public class ImportResult
    {
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int FailedRows { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<Contract> CreatedContracts { get; set; } = new();
        public List<User> CreatedUsers { get; set; } = new();
        public List<string> CreatedGroups { get; set; } = new();
        public List<string> CreatedPVs { get; set; } = new();
    }

    public interface IImportExecutionService
    {
        Task<ImportResult> ExecuteContractImportAsync(
            string uploadId,
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            string dateFormat,
            bool skipMissingContractNumber = false,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false);

        Task<ImportResult> ExecuteUserImportAsync(
            string uploadId,
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings);
            
        Task<ImportResult> ExecuteContractDashboardImportAsync(
            string uploadId,
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            bool skipMissingContractNumber = false,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false);

        Task<bool> UndoImportAsync(int importSessionId);
    }
}
