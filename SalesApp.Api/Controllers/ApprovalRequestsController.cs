using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/approval-requests")]
    [Authorize]
    public class ApprovalRequestsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;

        public ApprovalRequestsController(IApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        // POST: api/approval-requests
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ApprovalRequestResponse>>> Create([FromBody] CreateApprovalRequestDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var requesterId))
            {
                return Unauthorized(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            try
            {
                var result = await _approvalService.CreateAsync(requesterId, dto);
                return StatusCode(201, new ApiResponse<ApprovalRequestResponse>
                {
                    Success = true,
                    Data = result,
                    Message = "Solicitação criada com sucesso."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/approval-requests/pending
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<ApprovalRequestResponse>>>> GetPending()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var callerId))
            {
                return Unauthorized(new ApiResponse<List<ApprovalRequestResponse>>
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            var roleName = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            var results = await _approvalService.GetPendingAsync(callerId, roleName);
            return Ok(new ApiResponse<List<ApprovalRequestResponse>>
            {
                Success = true,
                Data = results,
                Message = "Solicitações pendentes obtidas com sucesso."
            });
        }

        // GET: api/approval-requests/mine
        [HttpGet("mine")]
        public async Task<ActionResult<ApiResponse<List<ApprovalRequestResponse>>>> GetMine()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var requesterId))
            {
                return Unauthorized(new ApiResponse<List<ApprovalRequestResponse>>
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            var results = await _approvalService.GetMyRequestsAsync(requesterId);
            return Ok(new ApiResponse<List<ApprovalRequestResponse>>
            {
                Success = true,
                Data = results,
                Message = "Minhas solicitações obtidas com sucesso."
            });
        }

        // POST: api/approval-requests/{id}/resolve
        [HttpPost("{id}/resolve")]
        public async Task<ActionResult<ApiResponse<ApprovalRequestResponse>>> Resolve(int id, [FromBody] ResolveApprovalDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var approverId))
            {
                return Unauthorized(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            var roleName = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            try
            {
                var result = await _approvalService.ResolveAsync(id, approverId, roleName, dto);
                return Ok(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = true,
                    Data = result,
                    Message = "Solicitação processada com sucesso."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return BadRequest(new ApiResponse<ApprovalRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}
