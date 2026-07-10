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
    }
}
