using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IMonitoringService
    {
        Task<List<MatriculaHealthResponse>> GetMatriculaHealthAsync();
        Task<List<TeamMatriculaHealthResponse>> GetEquipesHealthAsync();
        Task<List<AdminImportStatsResponse>> GetAdminImportStatsAsync();
        Task<LicensingReportResponse> GetLicensingReportAsync(int year, int month, int? minimumActiveDays);
        Task<ContractUpdateSummaryResponse> GetContractUpdateSummaryAsync(
            List<int>? teamIds = null,
            List<Guid>? userIds = null,
            List<string>? matriculas = null);
        Task<PagedContractResponse> GetContractsByUpdateAsync(
            string? updateFilter,
            List<int>? teamIds,
            List<Guid>? userIds,
            List<string>? matriculas,
            int page,
            int pageSize);
    }
}
