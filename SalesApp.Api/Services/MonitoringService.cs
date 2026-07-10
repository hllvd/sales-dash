using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SalesApp.DTOs;
using SalesApp.Models.Configuration;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class MonitoringService : IMonitoringService
    {
        private readonly IMonitoringRepository _monitoringRepository;
        private readonly LicensingOptions _licensingOptions;

        public MonitoringService(
            IMonitoringRepository monitoringRepository,
            IOptions<LicensingOptions> licensingOptions)
        {
            _monitoringRepository = monitoringRepository;
            _licensingOptions = licensingOptions.Value;
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

        public Task<LicensingReportResponse> GetLicensingReportAsync(int year, int month, int? minimumActiveDays)
        {
            int minDays = minimumActiveDays ?? _licensingOptions.DefaultMinimumActiveDays;
            return _monitoringRepository.GetLicensingReportAsync(
                year,
                month,
                minDays,
                _licensingOptions.ExcludedEmails,
                _licensingOptions.PriceTiers);
        }
    }
}
