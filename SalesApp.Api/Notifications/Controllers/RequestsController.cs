using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Notifications.DTOs;
using SalesApp.Notifications.Models;
using SalesApp.Notifications.Repositories;
using SalesApp.Notifications.Services;

namespace SalesApp.Notifications.Controllers
{
    [ApiController]
    [Route("api/requests")]
    [Authorize]
    public class RequestsController : ControllerBase
    {
        private readonly INotificationRepository _repository;
        private readonly INotificationQueue _queue;
        private readonly ILogger<RequestsController> _logger;

        public RequestsController(
            INotificationRepository repository,
            INotificationQueue queue,
            ILogger<RequestsController> logger)
        {
            _repository = repository;
            _queue = queue;
            _logger = logger;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        // GET: api/requests/pending
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<DomainRequestResponseDto>>>> GetPending()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<List<DomainRequestResponseDto>> { Success = false, Message = "Usuário não autenticado." });
            }

            var requests = await _repository.GetPendingRequestsAsync(userId);
            var dtos = requests.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<DomainRequestResponseDto>>
            {
                Success = true,
                Data = dtos,
                Message = "Solicitações pendentes obtidas com sucesso."
            });
        }

        // GET: api/requests/sent
        [HttpGet("sent")]
        public async Task<ActionResult<ApiResponse<List<DomainRequestResponseDto>>>> GetSent()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<List<DomainRequestResponseDto>> { Success = false, Message = "Usuário não autenticado." });
            }

            var requests = await _repository.GetSentRequestsAsync(userId);
            var dtos = requests.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<DomainRequestResponseDto>>
            {
                Success = true,
                Data = dtos,
                Message = "Solicitações enviadas obtidas com sucesso."
            });
        }

        // POST: api/requests/{sk}/accept
        // Atomic transaction: updates request status to ACCEPTED and marks linked notification as resolved
        [HttpPost("{sk}/accept")]
        public async Task<ActionResult<ApiResponse<bool>>> Accept(string sk, [FromBody] AcceptDeclineRequestDto? dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Usuário não autenticado." });
            }

            var requestSk = DecodeOrFormatSk(sk);

            // 1. Authorization check: fetch the request and verify caller is the intended recipient
            var request = await _repository.GetRequestAsync(userId, requestSk);
            if (request == null)
            {
                return NotFound(new ApiResponse<bool> { Success = false, Message = "Solicitação não encontrada." });
            }

            if (request.RecipientUserId != userId)
            {
                return StatusCode(403, new ApiResponse<bool> { Success = false, Message = "Você não tem autorização para aceitar esta solicitação." });
            }

            if (request.Status != RequestStatus.PENDING.ToString())
            {
                return BadRequest(new ApiResponse<bool> { Success = false, Message = $"Solicitação já resolvida com status: {request.Status}." });
            }

            // 2. Perform atomic TransactWriteItems
            var success = await _repository.ResolveRequestTransactAsync(
                userId,
                requestSk,
                RequestStatus.ACCEPTED.ToString(),
                resolvedBy: userId,
                relatedNotifSk: request.RelatedNotifSK);

            if (!success)
            {
                return Conflict(new ApiResponse<bool> { Success = false, Message = "Conflito: a solicitação já foi modificada por outra sessão." });
            }

            // 3. Real-time notification to the original requester that request was accepted
            if (!string.IsNullOrEmpty(request.RequesterUserId))
            {
                await _queue.EnqueueAsync((request.RequesterUserId, new SseEvent
                {
                    Event = "request_resolved",
                    Data = new
                    {
                        requestSk = request.SK,
                        status = RequestStatus.ACCEPTED.ToString(),
                        resolvedBy = userId
                    }
                }));
            }

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Solicitação aceita com sucesso."
            });
        }

        // POST: api/requests/{sk}/decline
        // Atomic transaction: updates request status to DECLINED and marks linked notification as resolved
        [HttpPost("{sk}/decline")]
        public async Task<ActionResult<ApiResponse<bool>>> Decline(string sk, [FromBody] AcceptDeclineRequestDto? dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Usuário não autenticado." });
            }

            var requestSk = DecodeOrFormatSk(sk);

            // 1. Authorization check
            var request = await _repository.GetRequestAsync(userId, requestSk);
            if (request == null)
            {
                return NotFound(new ApiResponse<bool> { Success = false, Message = "Solicitação não encontrada." });
            }

            if (request.RecipientUserId != userId)
            {
                return StatusCode(403, new ApiResponse<bool> { Success = false, Message = "Você não tem autorização para recusar esta solicitação." });
            }

            if (request.Status != RequestStatus.PENDING.ToString())
            {
                return BadRequest(new ApiResponse<bool> { Success = false, Message = $"Solicitação já resolvida com status: {request.Status}." });
            }

            // 2. Perform atomic TransactWriteItems
            var success = await _repository.ResolveRequestTransactAsync(
                userId,
                requestSk,
                RequestStatus.DECLINED.ToString(),
                resolvedBy: userId,
                relatedNotifSk: request.RelatedNotifSK);

            if (!success)
            {
                return Conflict(new ApiResponse<bool> { Success = false, Message = "Conflito: a solicitação já foi modificada por outra sessão." });
            }

            // 3. Real-time notification to the requester
            if (!string.IsNullOrEmpty(request.RequesterUserId))
            {
                await _queue.EnqueueAsync((request.RequesterUserId, new SseEvent
                {
                    Event = "request_resolved",
                    Data = new
                    {
                        requestSk = request.SK,
                        status = RequestStatus.DECLINED.ToString(),
                        resolvedBy = userId
                    }
                }));
            }

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Solicitação recusada com sucesso."
            });
        }

        private static string DecodeOrFormatSk(string raw)
        {
            var unescaped = Uri.UnescapeDataString(raw);
            return unescaped.StartsWith("REQUEST#") ? unescaped : $"REQUEST#{unescaped}";
        }

        private static DomainRequestResponseDto MapToDto(DomainRequest req)
        {
            return new DomainRequestResponseDto
            {
                Sk = req.SK,
                RecipientUserId = req.RecipientUserId,
                RequesterUserId = req.RequesterUserId,
                RequestType = req.RequestType,
                Status = req.Status,
                PayloadJson = req.PayloadJson,
                CreatedAt = req.CreatedAt,
                ResolvedAt = req.ResolvedAt
            };
        }
    }
}
