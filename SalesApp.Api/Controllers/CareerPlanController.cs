using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/career-plan")]
    [Authorize(Roles = "SuperAdmin,superadmin,Admin,admin")]
    public class CareerPlanController : ControllerBase
    {
        private readonly ICareerPlanService _careerPlanService;
        private readonly AppDbContext _context;
        private readonly ILogger<CareerPlanController> _logger;

        public CareerPlanController(
            ICareerPlanService careerPlanService,
            AppDbContext context,
            ILogger<CareerPlanController> logger)
        {
            _careerPlanService = careerPlanService;
            _context = context;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private bool IsSuperAdmin()
        {
            return User.IsInRole("SuperAdmin")
                || User.IsInRole("superadmin")
                || User.HasClaim("perm", "system:superadmin")
                || User.FindFirst("role_id")?.Value == "1";
        }

        private async Task<bool> CanAccessUserAsync(Guid targetUserId)
        {
            if (IsSuperAdmin()) return true;
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return false;
            if (targetUserId == currentUserId) return true;

            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            return targetUser != null && targetUser.ParentUserId == currentUserId;
        }

        /// <summary>
        /// Evaluates career plan progress and metrics for a specific user.
        /// Available to SuperAdmins and Admins (scoped to self and 1st-level subordinates).
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<ActionResult<ApiResponse<CareerPlanEvaluationResponse>>> EvaluateUser(
            Guid userId,
            [FromQuery] DateTime? asOfDate = null)
        {
            if (!await CanAccessUserAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = "Você não tem permissão para visualizar o plano de carreira deste usuário."
                });
            }

            try
            {
                var response = await _careerPlanService.EvaluateUserCareerPlanAsync(userId, asOfDate);
                return Ok(new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating career plan for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = "An unexpected error occurred while evaluating career plan."
                });
            }
        }

        /// <summary>
        /// Simulates career plan metrics for a specific user with temporary in-memory contract overrides.
        /// Does not mutate or persist any data.
        /// Available to SuperAdmins and Admins (scoped to self and 1st-level subordinates).
        /// </summary>
        [HttpPost("users/{userId}/simulate")]
        public async Task<ActionResult<ApiResponse<CareerPlanEvaluationResponse>>> SimulateUser(
            Guid userId,
            [FromBody] SimulateCareerPlanRequest request,
            [FromQuery] DateTime? asOfDate = null)
        {
            if (!await CanAccessUserAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = "Você não tem permissão para simular o plano de carreira deste usuário."
                });
            }

            try
            {
                var overrides = request?.Overrides ?? new List<ContractRtOverrideDto>();
                var response = await _careerPlanService.SimulateUserCareerPlanAsync(userId, overrides, asOfDate);
                return Ok(new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating career plan for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<CareerPlanEvaluationResponse>
                {
                    Success = false,
                    Message = "An unexpected error occurred while simulating career plan."
                });
            }
        }

        /// <summary>
        /// Returns summary of loaded career plan configuration.
        /// Available to SuperAdmins and Admins.
        /// </summary>
        [HttpGet("config")]
        public ActionResult<ApiResponse<CareerPlanConfigResponse>> GetConfig()
        {
            try
            {
                var summary = _careerPlanService.GetConfigSummary();
                return Ok(new ApiResponse<CareerPlanConfigResponse>
                {
                    Success = true,
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting career plan config summary");
                return StatusCode(500, new ApiResponse<CareerPlanConfigResponse>
                {
                    Success = false,
                    Message = "Failed to get career plan configuration."
                });
            }
        }

        /// <summary>
        /// Returns paginated contract RT tracking for users at or below Guimel.
        /// Available to SuperAdmins and Admins (scoped to self and 1st-level subordinates).
        /// </summary>
        [HttpGet("users/{userId}/contracts-rt")]
        public async Task<ActionResult<ApiResponse<ContractRtPageDto>>> GetContractsRt(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? asOfDate = null)
        {
            if (!await CanAccessUserAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "Você não tem permissão para visualizar os contratos deste usuário."
                });
            }

            if (page < 1)
            {
                return BadRequest(new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "Page must be greater than or equal to 1."
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "PageSize must be between 1 and 100."
                });
            }

            try
            {
                var response = await _careerPlanService.GetContractRtTrackingAsync(userId, page, pageSize, asOfDate);
                return Ok(new ApiResponse<ContractRtPageDto>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contracts RT for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "An unexpected error occurred while getting contract RT tracking."
                });
            }
        }

        /// <summary>
        /// Returns the pre-calculated career plan snapshot list of consultants (unclassified, Alef, Beth).
        /// For Admins, scoped strictly to self and 1st-level subordinates.
        /// </summary>
        [HttpGet("consultants")]
        public async Task<ActionResult<ApiResponse<CareerPlanConsultantsListResponse>>> GetConsultantsList()
        {
            try
            {
                var response = await _careerPlanService.GetConsultantsListAsync();

                if (!IsSuperAdmin())
                {
                    var currentUserId = GetCurrentUserId();
                    var directSubordinateIds = (await _context.Users
                        .Where(u => u.ParentUserId == currentUserId && u.IsActive)
                        .Select(u => u.Id)
                        .ToListAsync())
                        .ToHashSet();

                    var scopedConsultants = response.Consultants
                        .Where(c => c.UserId == currentUserId || directSubordinateIds.Contains(c.UserId))
                        .ToList();

                    response = new CareerPlanConsultantsListResponse
                    {
                        Consultants = scopedConsultants,
                        TotalCount = scopedConsultants.Count,
                        LastCalculatedAt = response.LastCalculatedAt
                    };
                }

                return Ok(new ApiResponse<CareerPlanConsultantsListResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting career plan consultants list");
                return StatusCode(500, new ApiResponse<CareerPlanConsultantsListResponse>
                {
                    Success = false,
                    Message = "An unexpected error occurred while getting career plan consultants list."
                });
            }
        }

        /// <summary>
        /// Triggers asynchronous/manual recalculation of career plan consultant snapshots in DynamoDB.
        /// Available to SuperAdmins and Admins.
        /// </summary>
        [HttpPost("consultants/recalculate")]
        public async Task<ActionResult<ApiResponse<RecalculateCareerPlanResponse>>> RecalculateConsultants()
        {
            try
            {
                var response = await _careerPlanService.RecalculateAllConsultantsSnapshotsAsync();
                return Ok(new ApiResponse<RecalculateCareerPlanResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating career plan consultants");
                return StatusCode(500, new ApiResponse<RecalculateCareerPlanResponse>
                {
                    Success = false,
                    Message = "An unexpected error occurred while recalculating career plan consultants."
                });
            }
        }
    }
}
