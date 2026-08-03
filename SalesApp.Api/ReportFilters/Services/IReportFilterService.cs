using SalesApp.ReportFilters.DTOs;
using SalesApp.ReportFilters.Validators;

namespace SalesApp.ReportFilters.Services
{
    public record ServiceResult<T>(bool Success, T? Data, List<ValidationError>? Errors = null, int StatusCode = 200);

    /// <summary>
    /// Business logic interface for saved report filters.
    /// </summary>
    public interface IReportFilterService
    {
        /// <summary>
        /// Returns shared reports + caller's own private reports.
        /// </summary>
        Task<ServiceResult<List<ReportFilterResponse>>> ListAsync(string callerId);

        /// <summary>
        /// Returns a single report. Returns 404 if not found or if private and not owned by caller.
        /// </summary>
        Task<ServiceResult<ReportFilterResponse>> GetAsync(string callerId, string filterId);

        /// <summary>
        /// Creates a new saved report. Caller must be superadmin.
        /// </summary>
        Task<ServiceResult<ReportFilterResponse>> CreateAsync(string callerId, CreateReportFilterRequest request);

        /// <summary>
        /// Replaces an existing report. Caller must be superadmin and owner.
        /// </summary>
        Task<ServiceResult<ReportFilterResponse>> UpdateAsync(string callerId, string filterId, UpdateReportFilterRequest request);

        /// <summary>
        /// Deletes a report. Caller must be superadmin and owner.
        /// </summary>
        Task<ServiceResult<bool>> DeleteAsync(string callerId, string filterId);

        /// <summary>
        /// Executes the saved report and returns paginated, projected contract results.
        /// Optional overrides replace configured Teams/Emails filter fields at query time without saving to DB.
        /// </summary>
        Task<ServiceResult<ReportResultsResponse>> ExecuteAsync(
            string callerId,
            string filterId,
            Guid? currentUserId,
            int page,
            int pageSize,
            List<int>? overrideTeamIds = null,
            List<string>? overrideEmails = null);

        /// <summary>
        /// Returns the full list of available columns grouped by source entity.
        /// </summary>
        AvailableColumnsResponse GetAvailableColumns();
    }
}
