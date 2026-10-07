using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Helpdesk.DTOs;

namespace SalesApp.Helpdesk.Services
{
    public interface IHelpdeskService
    {
        Task<HelpdeskQuestionResponse> CreateQuestionAsync(string userId, string userEmail, string userName, CreateHelpdeskQuestionRequest request);
        Task<HelpdeskQuestionResponse?> GetQuestionByIdAsync(string questionId, string callerUserId, bool isSuperAdmin);
        Task<HelpdeskQuestionResponse> UpdateQuestionContentAsync(string questionId, string callerUserId, UpdateHelpdeskQuestionRequest request);
        Task<HelpdeskQuestionResponse> UpdateStatusAsync(string questionId, string callerUserId, bool isSuperAdmin, UpdateHelpdeskStatusRequest request);
        Task<HelpdeskQuestionResponse> AddMessageAsync(string questionId, string callerUserId, string callerName, string callerRole, AddHelpdeskMessageRequest request);
        Task<List<HelpdeskQuestionResponse>> ListQuestionsAsync(string callerUserId, bool isSuperAdmin, string? statusFilter = null);
        Task<HelpdeskUnreadCountResponse> GetUnreadCountAsync(string callerUserId, bool isSuperAdmin);
    }
}
