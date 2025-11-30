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
    }

    public interface IImportExecutionService
    {
        Task<ImportResult> ExecuteContractImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings);

        Task<ImportResult> ExecuteUserImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings);
    }
}
