using System.IO;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IRetentionFilterService
    {
        Task<RetentionFilterProcessResponse> ProcessFilterPreviewAsync(Stream streamA, string fileNameA, Stream streamB, string fileNameB);
        Task<RetentionFilterExportResult> FilterAndGenerateWorkbookAsync(Stream streamA, string fileNameA, Stream streamB, string fileNameB);
    }
}
