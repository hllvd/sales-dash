using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SalesApp.Helpdesk.DTOs;
using SalesApp.Helpdesk.Models;
using SalesApp.Helpdesk.Repositories;

namespace SalesApp.Helpdesk.Services
{
    public class HelpdeskService : IHelpdeskService
    {
        private readonly IHelpdeskRepository _repository;
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepository;
        private readonly IGeminiLlmService _geminiLlmService;
        private readonly ILogger<HelpdeskService> _logger;
        private static readonly HashSet<string> ValidStatuses = new() { "Não lido", "Lida", "Pendente", "Resolvido" };

        public HelpdeskService(
            IHelpdeskRepository repository,
            IKnowledgeBaseRepository knowledgeBaseRepository,
            IGeminiLlmService geminiLlmService,
            ILogger<HelpdeskService> logger)
        {
            _repository = repository;
            _knowledgeBaseRepository = knowledgeBaseRepository;
            _geminiLlmService = geminiLlmService;
            _logger = logger;
        }

        public async Task<HelpdeskQuestionResponse> CreateQuestionAsync(string userId, string userEmail, string userName, CreateHelpdeskQuestionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("O título da pergunta é obrigatório.");

            if (string.IsNullOrWhiteSpace(request.Content))
                throw new ArgumentException("O conteúdo da pergunta é obrigatório.");

            var question = new HelpdeskQuestion
            {
                QuestionId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                UserEmail = userEmail,
                UserName = userName,
                Title = request.Title.Trim(),
                CurrentContent = request.Content.Trim(),
                SourcePageUrl = request.SourcePageUrl?.Trim() ?? "",
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                Status = "Não lido",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateQuestionAsync(question);
            return MapToResponse(question);
        }

        public async Task<HelpdeskQuestionResponse?> GetQuestionByIdAsync(string questionId, string callerUserId, bool isSuperAdmin)
        {
            var question = await _repository.GetQuestionByIdAsync(questionId);
            if (question == null) return null;

            if (!isSuperAdmin && question.UserId != callerUserId)
                throw new UnauthorizedAccessException("Você não tem permissão para visualizar esta pergunta.");

            return MapToResponse(question);
        }

        public async Task<HelpdeskQuestionResponse> UpdateQuestionContentAsync(string questionId, string callerUserId, UpdateHelpdeskQuestionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                throw new ArgumentException("O conteúdo não pode ser vazio.");

            var question = await _repository.GetQuestionByIdAsync(questionId);
            if (question == null)
                throw new KeyNotFoundException("Pergunta não encontrada.");

            if (question.UserId != callerUserId)
                throw new UnauthorizedAccessException("Apenas o autor pode editar a pergunta original.");

            var previousVersion = question.EditHistory.Count + 1;
            await _repository.UpdateQuestionContentAsync(questionId, request.Content.Trim(), question.CurrentContent, previousVersion);

            var updated = await _repository.GetQuestionByIdAsync(questionId);
            return MapToResponse(updated!);
        }

        public async Task<HelpdeskQuestionResponse> UpdateStatusAsync(string questionId, string callerUserId, bool isSuperAdmin, UpdateHelpdeskStatusRequest request)
        {
            if (!ValidStatuses.Contains(request.Status))
                throw new ArgumentException($"Status inválido. Permitidos: {string.Join(", ", ValidStatuses)}");

            var question = await _repository.GetQuestionByIdAsync(questionId);
            if (question == null)
                throw new KeyNotFoundException("Pergunta não encontrada.");

            if (!isSuperAdmin && question.UserId != callerUserId)
                throw new UnauthorizedAccessException("Você não tem permissão para alterar o status desta pergunta.");

            await _repository.UpdateStatusAsync(questionId, request.Status);

            var updated = await _repository.GetQuestionByIdAsync(questionId);

            if (request.Status == "Resolvido" && request.GenerateSummary && updated != null)
            {
                // Fire and forget in background so UI response is immediate
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Generating LLM summary for resolved question {QuestionId}...", questionId);
                        var summary = await _geminiLlmService.GenerateSummaryAsync(updated);
                        var article = new KnowledgeBaseArticle
                        {
                            ArticleId = Guid.NewGuid().ToString("N"),
                            QuestionId = updated.QuestionId,
                            Category = summary.Category,
                            Title = summary.Title,
                            Slug = summary.Slug,
                            SummaryContent = summary.Content,
                            Status = "published",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _knowledgeBaseRepository.SaveArticleAsync(article);
                        _logger.LogInformation("Saved KB article {Slug} under category {Category}", article.Slug, article.Category);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate or save background summary for question {QuestionId}", questionId);
                    }
                });
            }

            return MapToResponse(updated!);
        }

        public async Task<HelpdeskQuestionResponse> AddMessageAsync(string questionId, string callerUserId, string callerName, string callerRole, AddHelpdeskMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                throw new ArgumentException("A resposta não pode ser vazia.");

            var question = await _repository.GetQuestionByIdAsync(questionId);
            if (question == null)
                throw new KeyNotFoundException("Pergunta não encontrada.");

            var isSuperAdmin = callerRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);
            if (!isSuperAdmin && question.UserId != callerUserId)
                throw new UnauthorizedAccessException("Você não tem permissão para responder nesta conversa.");

            var msg = new HelpdeskMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                QuestionId = questionId,
                SenderUserId = callerUserId,
                SenderName = callerName,
                SenderRole = callerRole,
                Content = request.Content.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddMessageAsync(questionId, msg);

            var updated = await _repository.GetQuestionByIdAsync(questionId);
            return MapToResponse(updated!);
        }

        public async Task<List<HelpdeskQuestionResponse>> ListQuestionsAsync(string callerUserId, bool isSuperAdmin, string? statusFilter = null)
        {
            List<HelpdeskQuestion> list;
            if (isSuperAdmin)
            {
                list = await _repository.ListAllForSuperAdminAsync(statusFilter);
            }
            else
            {
                list = await _repository.ListByUserAsync(callerUserId);
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    list = list.Where(q => q.Status == statusFilter).ToList();
                }
            }

            return list.Select(MapToResponse).ToList();
        }

        public async Task<HelpdeskUnreadCountResponse> GetUnreadCountAsync(string callerUserId, bool isSuperAdmin)
        {
            var count = await _repository.GetUnreadCountAsync(callerUserId, isSuperAdmin);
            return new HelpdeskUnreadCountResponse { UnreadCount = count };
        }

        public async Task<List<KnowledgeBaseArticleResponse>> ListKnowledgeBaseArticlesAsync(string? category = null)
        {
            var articles = await _knowledgeBaseRepository.ListArticlesAsync(category);
            return articles.Select(MapToArticleResponse).ToList();
        }

        public async Task<KnowledgeBaseArticleResponse?> GetKnowledgeBaseArticleBySlugAsync(string slug)
        {
            var article = await _knowledgeBaseRepository.GetArticleBySlugAsync(slug);
            return article != null ? MapToArticleResponse(article) : null;
        }

        private static KnowledgeBaseArticleResponse MapToArticleResponse(KnowledgeBaseArticle a)
        {
            return new KnowledgeBaseArticleResponse
            {
                ArticleId = a.ArticleId,
                QuestionId = a.QuestionId,
                Category = a.Category,
                Title = a.Title,
                Slug = a.Slug,
                SummaryContent = a.SummaryContent,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            };
        }

        private static HelpdeskQuestionResponse MapToResponse(HelpdeskQuestion q)
        {
            return new HelpdeskQuestionResponse
            {
                QuestionId = q.QuestionId,
                UserId = q.UserId,
                UserEmail = q.UserEmail,
                UserName = q.UserName,
                Title = q.Title,
                CurrentContent = q.CurrentContent,
                SourcePageUrl = q.SourcePageUrl,
                ImageUrl = q.ImageUrl,
                Status = q.Status,
                CreatedAt = q.CreatedAt,
                UpdatedAt = q.UpdatedAt,
                Messages = q.Messages.Select(m => new HelpdeskMessageResponse
                {
                    MessageId = m.MessageId,
                    SenderUserId = m.SenderUserId,
                    SenderName = m.SenderName,
                    SenderRole = m.SenderRole,
                    Content = m.Content,
                    ImageUrl = m.ImageUrl,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                EditHistory = q.EditHistory.Select(h => new HelpdeskEditHistoryResponse
                {
                    Version = h.Version,
                    PreviousContent = h.PreviousContent,
                    EditedAt = h.EditedAt
                }).ToList()
            };
        }
    }
}
