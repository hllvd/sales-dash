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
    }
}
