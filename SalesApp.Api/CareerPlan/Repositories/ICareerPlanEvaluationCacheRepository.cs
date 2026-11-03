using System;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.CareerPlan.Repositories
{
    public interface ICareerPlanEvaluationCacheRepository
    {
        Task<CachedCareerPlanEvaluationDto?> GetAsync(Guid userId);
        Task SaveAsync(CachedCareerPlanEvaluationDto entry);
    }
}
