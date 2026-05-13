using SalesApp.ReportFilters.Models;

namespace SalesApp.ReportFilters.Repositories
{
    /// <summary>
    /// Repository contract for saved report filters.
    /// Implementations handle all DynamoDB I/O — no DynamoDB types leak beyond this boundary.
    /// </summary>
    public interface IReportFilterRepository
    {
        /// <summary>
        /// Returns all shared reports + all private reports owned by <paramref name="userId"/>.
        /// Uses the scope-createdAt-index GSI for shared reports — no Scan.
        /// </summary>
        Task<List<ReportFilter>> ListForUserAsync(string userId);

        /// <summary>
        /// Returns a single report by its filterId.
        /// Returns null if the item does not exist.
        /// </summary>
        Task<ReportFilter?> GetByIdAsync(string userId, string filterId);

        /// <summary>
        /// Creates a new report item in DynamoDB.
        /// </summary>
        Task CreateAsync(ReportFilter filter);

        /// <summary>
        /// Replaces an existing report item in DynamoDB.
        /// </summary>
        Task UpdateAsync(ReportFilter filter);

        /// <summary>
        /// Deletes a report item from DynamoDB.
        /// </summary>
        Task DeleteAsync(string userId, string filterId);
    }
}
