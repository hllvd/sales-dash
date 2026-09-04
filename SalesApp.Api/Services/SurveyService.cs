using System.Text.Json;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class SurveyService : ISurveyService
    {
        private readonly ISurveyRepository _surveyRepository;
        private readonly IUserRepository _userRepository;

        public SurveyService(ISurveyRepository surveyRepository, IUserRepository userRepository)
        {
            _surveyRepository = surveyRepository;
            _userRepository = userRepository;
        }

        public async Task<SurveySummaryDto> CreateAndDispatchAsync(CreateSurveyDto dto, Guid creatorId)
        {
            ValidateCreateSurveyDto(dto);

            var validUserIds = await GetValidActiveUserIdsAsync(dto.TargetUserIds);
            if (validUserIds.Count == 0)
            {
                throw new ArgumentException("Nenhum usuário válido e ativo encontrado para atribuição.");
            }

            var survey = new Survey
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                QuestionText = dto.QuestionText.Trim(),
                QuestionType = dto.QuestionType.ToLowerInvariant(),
                OptionsJson = dto.Options != null && dto.Options.Count > 0
                    ? JsonSerializer.Serialize(dto.Options.Select(o => o.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToList())
                    : null,
                CreatedByUserId = creatorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _surveyRepository.CreateAsync(survey);

            var now = DateTime.UtcNow;
            var assignments = validUserIds.Select(userId => new SurveyAssignment
            {
                SurveyId = survey.Id,
                UserId = userId,
                Status = "pending",
                SentAt = now,
                ExpiresAt = now.AddDays(2)
            }).ToList();

            await _surveyRepository.CreateAssignmentsBatchAsync(assignments);

            return MapToSummaryDto(survey, assignments.Count, 0, assignments.Count, 0);
        }

        public async Task<List<SurveySummaryDto>> GetAllSurveysAsync()
        {
            await _surveyRepository.ExpireStaleAssignmentsAsync();
            var surveys = await _surveyRepository.GetAllAsync();

            return surveys.Select(s =>
            {
                var assignments = s.Assignments.ToList();
                var answered = assignments.Count(a => a.Status == "answered");
                var pending = assignments.Count(a => a.Status == "pending");
                var expired = assignments.Count(a => a.Status == "expired");

                return MapToSummaryDto(s, assignments.Count, answered, pending, expired);
            }).ToList();
        }

        public async Task<SurveyResultDto> GetSurveyResultsAsync(Guid surveyId)
        {
            await _surveyRepository.ExpireStaleAssignmentsAsync();
            var survey = await _surveyRepository.GetByIdAsync(surveyId);
            if (survey == null)
            {
                throw new KeyNotFoundException("Pergunta não encontrada.");
            }

            var assignments = survey.Assignments.ToList();
            var answered = assignments.Count(a => a.Status == "answered");
            var pending = assignments.Count(a => a.Status == "pending");
            var expired = assignments.Count(a => a.Status == "expired");

            var summary = MapToSummaryDto(survey, assignments.Count, answered, pending, expired);

            var responses = assignments.Select(a => new SurveyIndividualResponseDto
            {
                AssignmentId = a.Id,
                UserId = a.UserId,
                UserName = a.User?.Name ?? "Usuário",
                UserEmail = a.User?.Email ?? string.Empty,
                Status = a.Status,
                Answer = a.Response?.Answer,
                AnsweredAt = a.Response?.AnsweredAt,
                SentAt = a.SentAt,
                ExpiresAt = a.ExpiresAt
            }).OrderBy(r => r.UserName).ToList();

            var aggregateCounts = CalculateAggregateCounts(survey.QuestionType, summary.Options, responses);

            return new SurveyResultDto
            {
                Summary = summary,
                AggregateCounts = aggregateCounts,
                Responses = responses
            };
        }

        public async Task<List<SurveyAssignmentDto>> GetPendingForUserAsync(Guid userId)
        {
            await _surveyRepository.ExpireStaleAssignmentsAsync();
            var pendingAssignments = await _surveyRepository.GetPendingForUserAsync(userId);

            return pendingAssignments.Select(a =>
            {
                var options = DeserializeOptions(a.Survey?.OptionsJson);
                return new SurveyAssignmentDto
                {
                    AssignmentId = a.Id,
                    SurveyId = a.SurveyId,
                    Title = a.Survey?.Title ?? string.Empty,
                    QuestionText = a.Survey?.QuestionText ?? string.Empty,
                    QuestionType = a.Survey?.QuestionType ?? "yesno",
                    Options = options,
                    SentAt = a.SentAt,
                    ExpiresAt = a.ExpiresAt
                };
            }).ToList();
        }

        public async Task AnswerAsync(Guid userId, AnswerSurveyDto dto)
        {
            if (dto.AssignmentId <= 0)
            {
                throw new ArgumentException("Id de atribuição inválido.");
            }

            if (string.IsNullOrWhiteSpace(dto.Answer))
            {
                throw new ArgumentException("A resposta não pode ser vazia.");
            }

            await _surveyRepository.ExpireStaleAssignmentsAsync();
            var assignment = await _surveyRepository.GetAssignmentByIdAsync(dto.AssignmentId);
            if (assignment == null)
            {
                throw new KeyNotFoundException("Atribuição de pergunta não encontrada.");
            }

            if (assignment.UserId != userId)
            {
                throw new UnauthorizedAccessException("Esta pergunta não pertence ao usuário atual.");
            }

            if (assignment.Status == "answered")
            {
                throw new InvalidOperationException("Esta pergunta já foi respondida.");
            }

            if (assignment.Status == "expired" || assignment.ExpiresAt <= DateTime.UtcNow)
            {
                assignment.Status = "expired";
                await _surveyRepository.UpdateAssignmentAsync(assignment);
                throw new InvalidOperationException("O prazo para responder esta pergunta expirou.");
            }

            var cleanAnswer = dto.Answer.Trim();

            var response = new SurveyResponse
            {
                SurveyAssignmentId = assignment.Id,
                UserId = userId,
                Answer = cleanAnswer,
                AnsweredAt = DateTime.UtcNow
            };

            await _surveyRepository.CreateResponseAsync(response);

            assignment.Status = "answered";
            await _surveyRepository.UpdateAssignmentAsync(assignment);
        }

        public async Task ResendAsync(Guid surveyId, ResendSurveyDto dto)
        {
            var survey = await _surveyRepository.GetByIdAsync(surveyId);
            if (survey == null)
            {
                throw new KeyNotFoundException("Pergunta não encontrada.");
            }

            var assignments = await _surveyRepository.GetAssignmentsBySurveyIdAsync(surveyId);

            List<SurveyAssignment> targetAssignments;
            if (dto.AssignmentIds != null && dto.AssignmentIds.Count > 0)
            {
                var targetIdSet = dto.AssignmentIds.ToHashSet();
                targetAssignments = assignments.Where(a => targetIdSet.Contains(a.Id)).ToList();
            }
            else
            {
                // Default: re-send to all non-answered assignments (pending or expired)
                targetAssignments = assignments.Where(a => a.Status != "answered").ToList();
            }

            if (targetAssignments.Count == 0)
            {
                return;
            }

            var assignmentIdsToReset = targetAssignments.Select(a => a.Id).ToList();
            await _surveyRepository.DeleteResponsesForAssignmentsAsync(assignmentIdsToReset);

            var now = DateTime.UtcNow;
            foreach (var a in targetAssignments)
            {
                a.Status = "pending";
                a.SentAt = now;
                a.ExpiresAt = now.AddDays(2);
            }

            await _surveyRepository.UpdateAssignmentsBatchAsync(targetAssignments);
        }

        public async Task<List<UserSurveyHistoryDto>> GetUserHistoryAsync(Guid userId)
        {
            await _surveyRepository.ExpireStaleAssignmentsAsync();
            var assignments = await _surveyRepository.GetUserHistoryAsync(userId);

            return assignments.Select(a => new UserSurveyHistoryDto
            {
                AssignmentId = a.Id,
                SurveyId = a.SurveyId,
                Title = a.Survey?.Title ?? string.Empty,
                QuestionText = a.Survey?.QuestionText ?? string.Empty,
                QuestionType = a.Survey?.QuestionType ?? "yesno",
                Status = a.Status,
                Answer = a.Response?.Answer,
                SentAt = a.SentAt,
                ExpiresAt = a.ExpiresAt,
                AnsweredAt = a.Response?.AnsweredAt
            }).ToList();
        }

        // --- Private Helper Functions (Pure logic) ---

        private static void ValidateCreateSurveyDto(CreateSurveyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                throw new ArgumentException("O título da pergunta é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(dto.QuestionText))
            {
                throw new ArgumentException("O texto da pergunta é obrigatório.");
            }

            var type = dto.QuestionType?.ToLowerInvariant();
            if (type != "yesno" && type != "singlechoice" && type != "multichoice")
            {
                throw new ArgumentException("Tipo de pergunta inválido. Use: yesno, singlechoice ou multichoice.");
            }

            if ((type == "singlechoice" || type == "multichoice") &&
                (dto.Options == null || dto.Options.Count(o => !string.IsNullOrWhiteSpace(o)) < 2))
            {
                throw new ArgumentException("Perguntas de escolha múltipla ou única exigem pelo menos duas opções.");
            }

            if (dto.TargetUserIds == null || dto.TargetUserIds.Count == 0)
            {
                throw new ArgumentException("Pelo menos um usuário deve ser selecionado.");
            }
        }

        private async Task<List<Guid>> GetValidActiveUserIdsAsync(List<Guid> candidateUserIds)
        {
            var distinctIds = candidateUserIds.Distinct().ToList();
            var validIds = new List<Guid>();

            foreach (var id in distinctIds)
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user != null && user.IsActive)
                {
                    validIds.Add(user.Id);
                }
            }

            return validIds;
        }

        private static SurveySummaryDto MapToSummaryDto(Survey s, int totalAssigned, int answered, int pending, int expired)
        {
            return new SurveySummaryDto
            {
                Id = s.Id,
                Title = s.Title,
                QuestionText = s.QuestionText,
                QuestionType = s.QuestionType,
                Options = DeserializeOptions(s.OptionsJson),
                CreatedAt = s.CreatedAt,
                TotalAssigned = totalAssigned,
                TotalAnswered = answered,
                TotalPending = pending,
                TotalExpired = expired
            };
        }

        private static List<string>? DeserializeOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json);
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, int> CalculateAggregateCounts(
            string questionType,
            List<string>? options,
            List<SurveyIndividualResponseDto> responses)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (questionType == "yesno")
            {
                counts["Sim"] = 0;
                counts["Não"] = 0;
            }
            else if (options != null)
            {
                foreach (var opt in options)
                {
                    counts[opt] = 0;
                }
            }

            foreach (var r in responses)
            {
                if (r.Status != "answered" || string.IsNullOrWhiteSpace(r.Answer)) continue;

                if (questionType == "multichoice")
                {
                    // Multi-choice answers may be comma-separated or JSON list
                    List<string> selectedList;
                    try
                    {
                        selectedList = JsonSerializer.Deserialize<List<string>>(r.Answer) ?? new List<string>();
                    }
                    catch
                    {
                        selectedList = r.Answer.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    }

                    foreach (var item in selectedList)
                    {
                        if (counts.ContainsKey(item))
                            counts[item]++;
                        else
                            counts[item] = 1;
                    }
                }
                else
                {
                    var normalizedKey = r.Answer.Trim();
                    if (counts.ContainsKey(normalizedKey))
                        counts[normalizedKey]++;
                    else
                        counts[normalizedKey] = 1;
                }
            }

            return counts;
        }
    }
}
