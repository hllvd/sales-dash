using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.Repositories
{
    public interface IMonitoringRepository
    {
        Task<List<MatriculaHealthResponse>> GetMatriculaHealthAsync();
        Task<List<TeamMatriculaHealthResponse>> GetEquipesHealthAsync();
        Task<List<AdminImportStatsResponse>> GetAdminImportStatsAsync();
        Task<LicensingReportResponse> GetLicensingReportAsync(
            int year,
            int month,
            int minimumActiveDays,
            List<string> excludedEmails,
            List<SalesApp.Models.Configuration.PriceTier> priceTiers);
        Task<ContractUpdateSummaryResponse> GetContractUpdateSummaryAsync(
            List<int>? teamIds = null,
            List<Guid>? userIds = null,
            List<string>? matriculas = null,
            DateTime? startDate = null,
            DateTime? endDate = null);
        Task<(List<ContractResponse> Items, int TotalCount)> GetContractsByUpdateAsync(
            string? updateFilter,
            List<int>? teamIds,
            List<Guid>? userIds,
            List<string>? matriculas,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize);
    }
}
