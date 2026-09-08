using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface ISurveyRepository
    {
        Task<Survey?> GetByIdAsync(Guid id);
        Task<List<Survey>> GetAllAsync();
        Task<Survey> CreateAsync(Survey survey);
        Task CreateAssignmentsBatchAsync(IEnumerable<SurveyAssignment> assignments);
        Task<List<SurveyAssignment>> GetPendingForUserAsync(Guid userId);
        Task<SurveyAssignment?> GetAssignmentByIdAsync(int assignmentId);
        Task<List<SurveyAssignment>> GetAssignmentsBySurveyIdAsync(Guid surveyId);
        Task<SurveyResponse> CreateResponseAsync(SurveyResponse response);
        Task UpdateAssignmentAsync(SurveyAssignment assignment);
        Task UpdateAssignmentsBatchAsync(IEnumerable<SurveyAssignment> assignments);
        Task ExpireStaleAssignmentsAsync();
        Task<List<SurveyAssignment>> GetUserHistoryAsync(Guid userId);
        Task DeleteResponsesForAssignmentsAsync(IEnumerable<int> assignmentIds);
    }
}
