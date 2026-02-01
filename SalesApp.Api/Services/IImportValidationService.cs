namespace SalesApp.Services
{
    public interface IImportValidationService
    {
        Task<List<string>> ValidateRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, string entityType, List<string>? requiredFields = null, bool allowAutoCreateGroups = false);
        Task<Dictionary<int, List<string>>> ValidateAllRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, string> mappings, string entityType, List<string>? requiredFields = null, bool allowAutoCreateGroups = false);
    }
}
