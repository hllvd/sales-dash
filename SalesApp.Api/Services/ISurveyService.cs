using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface ISurveyService
    {
        Task<SurveySummaryDto> CreateAndDispatchAsync(CreateSurveyDto dto, Guid creatorId);
        Task<List<SurveySummaryDto>> GetAllSurveysAsync();
        Task<SurveyResultDto> GetSurveyResultsAsync(Guid surveyId);
        Task<List<SurveyAssignmentDto>> GetPendingForUserAsync(Guid userId);
        Task AnswerAsync(Guid userId, AnswerSurveyDto dto);
        Task ResendAsync(Guid surveyId, ResendSurveyDto dto);
        Task<List<UserSurveyHistoryDto>> GetUserHistoryAsync(Guid userId);
    }
}
