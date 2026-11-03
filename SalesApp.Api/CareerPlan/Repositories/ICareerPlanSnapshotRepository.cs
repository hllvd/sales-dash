using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.DTOs;

namespace SalesApp.CareerPlan.Repositories
{
    public interface ICareerPlanSnapshotRepository
    {
        Task<IReadOnlyList<CareerPlanConsultantSnapshotDto>> GetLatestSnapshotsAsync();
        Task SaveSnapshotsAsync(IReadOnlyList<CareerPlanConsultantSnapshotDto> snapshots);
    }
}
