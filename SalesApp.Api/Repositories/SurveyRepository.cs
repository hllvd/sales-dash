using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public class SurveyRepository : ISurveyRepository
    {
        private readonly AppDbContext _context;

        public SurveyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Survey?> GetByIdAsync(Guid id)
        {
            return await _context.Surveys
                .Include(s => s.CreatedBy)
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.User)
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.Response)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<Survey>> GetAllAsync()
        {
            return await _context.Surveys
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.Response)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<Survey> CreateAsync(Survey survey)
        {
            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();
            return survey;
        }

        public async Task CreateAssignmentsBatchAsync(IEnumerable<SurveyAssignment> assignments)
        {
            _context.SurveyAssignments.AddRange(assignments);
            await _context.SaveChangesAsync();
        }

        public async Task<List<SurveyAssignment>> GetPendingForUserAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            return await _context.SurveyAssignments
                .Include(a => a.Survey)
                .Where(a => a.UserId == userId && a.Status == "pending" && a.ExpiresAt > now)
                .OrderBy(a => a.SentAt)
                .ToListAsync();
        }

        public async Task<SurveyAssignment?> GetAssignmentByIdAsync(int assignmentId)
        {
            return await _context.SurveyAssignments
                .Include(a => a.Survey)
                .Include(a => a.Response)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);
        }

        public async Task<List<SurveyAssignment>> GetAssignmentsBySurveyIdAsync(Guid surveyId)
        {
            return await _context.SurveyAssignments
                .Include(a => a.User)
                .Include(a => a.Response)
                .Where(a => a.SurveyId == surveyId)
                .OrderBy(a => a.SentAt)
                .ToListAsync();
        }

        public async Task<SurveyResponse> CreateResponseAsync(SurveyResponse response)
        {
            _context.SurveyResponses.Add(response);
            await _context.SaveChangesAsync();
            return response;
        }

        public async Task UpdateAssignmentAsync(SurveyAssignment assignment)
        {
            _context.SurveyAssignments.Update(assignment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAssignmentsBatchAsync(IEnumerable<SurveyAssignment> assignments)
        {
            _context.SurveyAssignments.UpdateRange(assignments);
            await _context.SaveChangesAsync();
        }

        public async Task ExpireStaleAssignmentsAsync()
        {
            var now = DateTime.UtcNow;
            var stale = await _context.SurveyAssignments
                .Where(a => a.Status == "pending" && a.ExpiresAt <= now)
                .ToListAsync();

            if (stale.Count > 0)
            {
                foreach (var a in stale)
                {
                    a.Status = "expired";
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<SurveyAssignment>> GetUserHistoryAsync(Guid userId)
        {
            return await _context.SurveyAssignments
                .Include(a => a.Survey)
                .Include(a => a.Response)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.SentAt)
                .ToListAsync();
        }

        public async Task DeleteResponsesForAssignmentsAsync(IEnumerable<int> assignmentIds)
        {
            var idList = assignmentIds.ToList();
            if (idList.Count == 0) return;

            var responses = await _context.SurveyResponses
                .Where(r => idList.Contains(r.SurveyAssignmentId))
                .ToListAsync();

            if (responses.Count > 0)
            {
                _context.SurveyResponses.RemoveRange(responses);
                await _context.SaveChangesAsync();
            }
        }
    }
}
