using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IExportService
    {
        /// <summary>
        /// Queues an async XLSX export job for the given filters and scope.
        /// Returns the jobId immediately; processing happens in background.
        /// </summary>
        string StartExport(ContractExportRequest filters, UserScopeContext scope, string requestingUserId);

        /// <summary>
        /// Returns current status of the export job.
        /// Returns null if the job does not exist or has expired (>10 minutes).
        /// </summary>
        ExportJobResponse? GetJobStatus(string jobId);

        /// <summary>
        /// Returns the generated XLSX bytes if the job is completed.
        /// Returns null if not found, expired, or not yet complete.
        /// </summary>
        byte[]? GetJobBytes(string jobId, string requestingUserId);

        /// <summary>
        /// Queues an async XLSX export job for a saved report.
        /// </summary>
        string StartReportExport(string filterId, string requestingUserId);
    }
}
