using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Utils;

namespace SalesApp.Services
{
    public class ApprovalService : IApprovalService
    {
        private readonly AppDbContext _context;
        private readonly IUserHierarchyService _hierarchyService;

        public ApprovalService(AppDbContext context, IUserHierarchyService hierarchyService)
        {
            _context = context;
            _hierarchyService = hierarchyService;
        }

        public async Task<ApprovalRequestResponse> CreateAsync(Guid requesterId, CreateApprovalRequestDto dto)
        {
            var requester = await _context.Users.FirstOrDefaultAsync(u => u.Id == requesterId && u.IsActive);
            if (requester == null)
            {
                throw new ArgumentException("Solicitante não encontrado ou inativo.");
            }

            // Validate request type and payload
            if (dto.RequestType == "ChangeParentEmail")
            {
                var payload = JsonSerializer.Deserialize<ChangeParentEmailPayload>(dto.PayloadJson);
                if (string.IsNullOrWhiteSpace(payload?.NewParentEmail))
                {
                    throw new ArgumentException("E-mail do novo superior é obrigatório.");
                }

                var parentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == payload.NewParentEmail.Trim().ToLower() && u.IsActive);
                if (parentUser == null)
                {
                    throw new ArgumentException($"Usuário com o e-mail '{payload.NewParentEmail}' não foi encontrado ou está inativo.");
                }

                var validationError = await _hierarchyService.ValidateHierarchyChangeAsync(requesterId, parentUser.Id);
                if (!string.IsNullOrEmpty(validationError))
                {
                    throw new InvalidOperationException(validationError);
                }
            }
            else if (dto.RequestType == "RequestMatricula" || dto.RequestType == "AdminRequestMatricula")
            {
                var payload = JsonSerializer.Deserialize<RequestMatriculaPayload>(dto.PayloadJson);
                if (string.IsNullOrWhiteSpace(payload?.MatriculaNumber))
                {
                    throw new ArgumentException("Número da matrícula é obrigatório.");
                }

                var normalizedNumber = NormalizationUtils.NormalizeNumber(payload.MatriculaNumber);
                if (string.IsNullOrEmpty(normalizedNumber))
                {
                    throw new ArgumentException("Número da matrícula inválido.");
                }
            }
            else
            {
                throw new ArgumentException($"Tipo de solicitação inválido: {dto.RequestType}");
            }

            var request = new ApprovalRequest
            {
                RequestType = dto.RequestType,
                RequesterId = requesterId,
                Status = "Pending",
                PayloadJson = dto.PayloadJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ApprovalRequests.Add(request);
            await _context.SaveChangesAsync();

            // Re-fetch with nav properties
            var created = await _context.ApprovalRequests
                .Include(r => r.Requester)
                .FirstAsync(r => r.Id == request.Id);

            return MapToResponse(created);
        }

        public async Task<List<ApprovalRequestResponse>> GetPendingAsync(Guid callerId, string callerRole)
        {
            var isSuperAdmin = callerRole.ToLower() == "superadmin" || callerRole == "1";

            var query = _context.ApprovalRequests
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .Where(r => r.Status == "Pending");

            if (!isSuperAdmin)
            {
                var descendantIds = await _hierarchyService.GetDescendantIdsAsync(callerId);
                query = query.Where(r => r.RequestType != "AdminRequestMatricula" && descendantIds.Contains(r.RequesterId));
            }

            var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return list.Select(MapToResponse).ToList();
        }

        public async Task<List<ApprovalRequestResponse>> GetMyRequestsAsync(Guid requesterId)
        {
            var list = await _context.ApprovalRequests
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .Where(r => r.RequesterId == requesterId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return list.Select(MapToResponse).ToList();
        }

        public async Task<ApprovalRequestResponse> ResolveAsync(int requestId, Guid approverId, string approverRole, ResolveApprovalDto dto)
        {
            var request = await _context.ApprovalRequests
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                throw new KeyNotFoundException("Solicitação não encontrada.");
            }

            if (request.Status != "Pending")
            {
                throw new InvalidOperationException("Esta solicitação já foi processada.");
            }

            var isSuperAdmin = approverRole.ToLower() == "superadmin" || approverRole == "1";

            if (request.RequestType == "AdminRequestMatricula" && !isSuperAdmin)
            {
                throw new UnauthorizedAccessException("Apenas SuperAdmins podem aprovar solicitações de matrícula para Administradores.");
            }

            if (!isSuperAdmin)
            {
                var descendantIds = await _hierarchyService.GetDescendantIdsAsync(approverId);
                if (!descendantIds.Contains(request.RequesterId))
                {
                    throw new UnauthorizedAccessException("Você não possui permissão para aprovar solicitações deste usuário.");
                }
            }

            var actionUpper = dto.Action.Trim().ToUpperInvariant();

            if (actionUpper == "APPROVED" || actionUpper == "YES" || actionUpper == "APROVAR")
            {
                request.Status = "Approved";
                request.ApproverId = approverId;
                request.ApproverComment = dto.Comment;
                request.UpdatedAt = DateTime.UtcNow;

                // Execute action immediately (One-step approval)
                await ExecuteActionAsync(request);

                // TODO: notify requester via email/in-app notification
            }
            else if (actionUpper == "REJECTED" || actionUpper == "NO" || actionUpper == "REJEITAR")
            {
                request.Status = "Rejected";
                request.ApproverId = approverId;
                request.ApproverComment = dto.Comment;
                request.UpdatedAt = DateTime.UtcNow;

                // TODO: notify requester via email/in-app notification
            }
            else if (actionUpper == "LATER" || actionUpper == "DEPOIS")
            {
                // Stays Pending indefinitely as requested
                request.ApproverComment = dto.Comment;
                request.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                throw new ArgumentException($"Ação inválida: {dto.Action}");
            }

            await _context.SaveChangesAsync();

            var updated = await _context.ApprovalRequests
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .FirstAsync(r => r.Id == request.Id);

            return MapToResponse(updated);
        }

        private async Task ExecuteActionAsync(ApprovalRequest request)
        {
            var requester = await _context.Users.FirstAsync(u => u.Id == request.RequesterId);

            if (request.RequestType == "ChangeParentEmail")
            {
                var payload = JsonSerializer.Deserialize<ChangeParentEmailPayload>(request.PayloadJson);
                if (payload == null || string.IsNullOrWhiteSpace(payload.NewParentEmail))
                {
                    throw new InvalidOperationException("Payload de alteração de e-mail do superior inválido.");
                }

                var parentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == payload.NewParentEmail.Trim().ToLower() && u.IsActive);
                if (parentUser == null)
                {
                    throw new InvalidOperationException($"Usuário superior com e-mail '{payload.NewParentEmail}' não encontrado.");
                }

                var validationError = await _hierarchyService.ValidateHierarchyChangeAsync(request.RequesterId, parentUser.Id);
                if (!string.IsNullOrEmpty(validationError))
                {
                    throw new InvalidOperationException(validationError);
                }

                requester.ParentUserId = parentUser.Id;
                requester.UpdatedAt = DateTime.UtcNow;
            }
            else if (request.RequestType == "RequestMatricula" || request.RequestType == "AdminRequestMatricula")
            {
                var payload = JsonSerializer.Deserialize<RequestMatriculaPayload>(request.PayloadJson);
                if (payload == null || string.IsNullOrWhiteSpace(payload.MatriculaNumber))
                {
                    throw new InvalidOperationException("Payload de solicitação de matrícula inválido.");
                }

                var normNumber = NormalizationUtils.NormalizeNumber(payload.MatriculaNumber);

                var matricula = await _context.Matriculas.FirstOrDefaultAsync(m => m.MatriculaNumber == normNumber);
                if (matricula == null)
                {
                    matricula = new Matricula
                    {
                        MatriculaNumber = normNumber,
                        Status = "active",
                        StartDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Matriculas.Add(matricula);
                    await _context.SaveChangesAsync();
                }

                var isOwner = request.RequestType == "AdminRequestMatricula";

                var existingLink = await _context.UserMatriculas
                    .FirstOrDefaultAsync(um => um.UserInternalId == requester.InternalId && um.MatriculaId == matricula.Id);

                if (existingLink == null)
                {
                    var userMatricula = new UserMatricula
                    {
                        UserInternalId = requester.InternalId,
                        MatriculaId = matricula.Id,
                        IsOwner = isOwner,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserMatriculas.Add(userMatricula);
                }
                else
                {
                    existingLink.IsOwner = isOwner;
                    existingLink.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        private static ApprovalRequestResponse MapToResponse(ApprovalRequest request)
        {
            return new ApprovalRequestResponse
            {
                Id = request.Id,
                RequestType = request.RequestType,
                RequesterId = request.RequesterId,
                RequesterName = request.Requester?.Name ?? string.Empty,
                RequesterEmail = request.Requester?.Email ?? string.Empty,
                ApproverId = request.ApproverId,
                ApproverName = request.Approver?.Name,
                Status = request.Status,
                PayloadJson = request.PayloadJson,
                ApproverComment = request.ApproverComment,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt
            };
        }
    }
}
