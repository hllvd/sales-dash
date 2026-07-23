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
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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
                var payload = JsonSerializer.Deserialize<ChangeParentEmailPayload>(dto.PayloadJson, _jsonOptions);
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
                var payload = JsonSerializer.Deserialize<RequestMatriculaPayload>(dto.PayloadJson, _jsonOptions);
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
            var callerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == callerId && u.IsActive);
            if (callerUser == null) return new List<ApprovalRequestResponse>();

            var list = await _context.ApprovalRequests
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var allowed = new List<ApprovalRequest>();
            foreach (var req in list)
            {
                if (await CanUserApproveRequestAsync(req, callerId, callerRole, callerUser))
                {
                    allowed.Add(req);
                }
            }

            return allowed.Select(MapToResponse).ToList();
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

            var approverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == approverId && u.IsActive);
            if (approverUser == null)
            {
                throw new UnauthorizedAccessException("Usuário aprovador não encontrado ou inativo.");
            }

            if (!await CanUserApproveRequestAsync(request, approverId, approverRole, approverUser))
            {
                throw new UnauthorizedAccessException("Você não possui permissão para aprovar ou rejeitar esta solicitação.");
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

        private async Task<bool> CanUserApproveRequestAsync(ApprovalRequest request, Guid callerId, string callerRole, User callerUser)
        {
            var isSuperAdmin = callerRole.ToLower() == "superadmin" || callerRole == "1";
            if (isSuperAdmin) return true;

            // 1. ChangeParentEmail
            if (request.RequestType == "ChangeParentEmail")
            {
                var payload = JsonSerializer.Deserialize<ChangeParentEmailPayload>(request.PayloadJson, _jsonOptions);
                if (payload != null && !string.IsNullOrWhiteSpace(payload.NewParentEmail))
                {
                    var targetParent = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Email.ToLower() == payload.NewParentEmail.Trim().ToLower() && u.IsActive);

                    if (targetParent != null)
                    {
                        var isParentAdmin = targetParent.RoleId == 2 ||
                                            (targetParent.Role != null && targetParent.Role.Name.ToLower() == "admin");
                        if (isParentAdmin)
                        {
                            // If the target parentEmail is an admin, ONLY that parentEmail user (and superadmins) can accept/deny
                            return callerId == targetParent.Id;
                        }
                    }
                }
            }

            // 2. RequestMatricula (Nova Matrícula Usuário)
            if (request.RequestType == "RequestMatricula")
            {
                var payload = JsonSerializer.Deserialize<RequestMatriculaPayload>(request.PayloadJson, _jsonOptions);
                if (payload != null && !string.IsNullOrWhiteSpace(payload.MatriculaNumber))
                {
                    var normNumber = NormalizationUtils.NormalizeNumber(payload.MatriculaNumber);
                    var matricula = await _context.Matriculas.FirstOrDefaultAsync(m => m.MatriculaNumber == normNumber);
                    if (matricula != null)
                    {
                        var ownerLink = await _context.UserMatriculas
                            .Include(um => um.User)
                            .FirstOrDefaultAsync(um => um.MatriculaId == matricula.Id && um.IsOwner && um.IsActive);

                        if (ownerLink != null && ownerLink.User != null)
                        {
                            // Only the owner of the matrícula (and superadmins) can accept/deny it
                            return ownerLink.User.Id == callerId;
                        }
                    }
                }
            }

            // 3. AdminRequestMatricula (Only SuperAdmins)
            if (request.RequestType == "AdminRequestMatricula")
            {
                return false;
            }

            // Fallback: Hierarchy check (requester is descendant of caller)
            var descendantIds = await _hierarchyService.GetDescendantIdsAsync(callerId);
            return descendantIds.Contains(request.RequesterId);
        }

        private async Task ExecuteActionAsync(ApprovalRequest request)
        {
            var requester = await _context.Users.FirstAsync(u => u.Id == request.RequesterId);

            if (request.RequestType == "ChangeParentEmail")
            {
                var payload = JsonSerializer.Deserialize<ChangeParentEmailPayload>(request.PayloadJson, _jsonOptions);
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
                var payload = JsonSerializer.Deserialize<RequestMatriculaPayload>(request.PayloadJson, _jsonOptions);
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
