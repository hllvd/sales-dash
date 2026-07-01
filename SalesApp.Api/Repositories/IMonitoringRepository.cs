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
    }
}
