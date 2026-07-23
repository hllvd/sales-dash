using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IApprovalService
    {
        Task<ApprovalRequestResponse> CreateAsync(Guid requesterId, CreateApprovalRequestDto dto);
        Task<List<ApprovalRequestResponse>> GetPendingAsync(Guid callerId, string callerRole);
        Task<List<ApprovalRequestResponse>> GetMyRequestsAsync(Guid requesterId);
        Task<ApprovalRequestResponse> ResolveAsync(int requestId, Guid approverId, string approverRole, ResolveApprovalDto dto);
    }
}
