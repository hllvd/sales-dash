using System;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface ICareerPlanService
    {
        Task<CareerPlanEvaluationResponse> EvaluateUserCareerPlanAsync(Guid userId, DateTime? asOfDate = null);
        CareerPlanConfigResponse GetConfigSummary();
        Task<ContractRtPageDto> GetContractRtTrackingAsync(Guid userId, int page = 1, int pageSize = 100, DateTime? asOfDate = null);
    }
}
