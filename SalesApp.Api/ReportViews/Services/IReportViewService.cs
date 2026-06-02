using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.ReportViews.DTOs;

namespace SalesApp.ReportViews.Services
{
    public record ServiceResult<T>(bool Success, T? Data, List<string>? Errors = null, int StatusCode = 200);

    public interface IReportViewService
    {
        Task<ServiceResult<List<ReportViewResponse>>> ListAsync(string callerId);
        Task<ServiceResult<ReportViewResponse>> GetAsync(string callerId, string viewId);
        Task<ServiceResult<ReportViewResponse>> CreateAsync(string callerId, CreateReportViewRequest request);
        Task<ServiceResult<ReportViewResponse>> UpdateAsync(string callerId, string viewId, UpdateReportViewRequest request);
        Task<ServiceResult<bool>> DeleteAsync(string callerId, string viewId);
    }
}
