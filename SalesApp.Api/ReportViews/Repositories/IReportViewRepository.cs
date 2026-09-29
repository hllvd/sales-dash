using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.ReportViews.Models;

namespace SalesApp.ReportViews.Repositories
{
    public interface IReportViewRepository
    {
        Task<List<ReportView>> ListForUserAsync(string userId);
        Task<List<ReportView>> ListAllAsync();
        Task<ReportView?> GetByIdAsync(string userId, string viewId);
        Task<ReportView?> GetAnyByIdAsync(string viewId);
        Task CreateAsync(ReportView view);
        Task UpdateAsync(ReportView view);
        Task DeleteAsync(string userId, string viewId);
    }
}
