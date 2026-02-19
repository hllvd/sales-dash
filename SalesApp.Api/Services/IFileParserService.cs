namespace SalesApp.Services
{
    public interface IFileParserService
    {
        Task<List<Dictionary<string, string>>> ParseFileAsync(IFormFile file);
        IAsyncEnumerable<Dictionary<string, string>> ParseFileStreamedAsync(IFormFile file);
        string GetFileType(IFormFile file);
        Task<List<string>> GetColumnsAsync(IFormFile file);
    }
}
