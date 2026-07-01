using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class MonitoringService : IMonitoringService
    {
        private readonly IMonitoringRepository _monitoringRepository;

        public MonitoringService(IMonitoringRepository monitoringRepository)
        {
            _monitoringRepository = monitoringRepository;
        }

        public Task<List<MatriculaHealthResponse>> GetMatriculaHealthAsync()
        {
            return _monitoringRepository.GetMatriculaHealthAsync();
        }

        public Task<List<TeamMatriculaHealthResponse>> GetEquipesHealthAsync()
        {
            return _monitoringRepository.GetEquipesHealthAsync();
        }

        public Task<List<AdminImportStatsResponse>> GetAdminImportStatsAsync()
        {
            return _monitoringRepository.GetAdminImportStatsAsync();
        }
    }
}
