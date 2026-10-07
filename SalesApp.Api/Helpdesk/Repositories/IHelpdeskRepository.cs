using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Helpdesk.Models;

namespace SalesApp.Helpdesk.Repositories
{
    public interface IHelpdeskRepository
    {
        Task CreateQuestionAsync(HelpdeskQuestion question);
        Task<HelpdeskQuestion?> GetQuestionByIdAsync(string questionId);
        Task UpdateQuestionContentAsync(string questionId, string newContent, string previousContent, int version);
        Task UpdateStatusAsync(string questionId, string newStatus);
        Task AddMessageAsync(string questionId, HelpdeskMessage message);
        Task<List<HelpdeskQuestion>> ListByUserAsync(string userId);
        Task<List<HelpdeskQuestion>> ListAllForSuperAdminAsync(string? statusFilter = null);
        Task<int> GetUnreadCountAsync(string userId, bool isSuperAdmin);
    }
}
