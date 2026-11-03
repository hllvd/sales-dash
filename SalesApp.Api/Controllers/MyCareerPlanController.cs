using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/career-plan/me")]
    [Authorize(Roles = "user,User,admin,Admin")]
    public class MyCareerPlanController : ControllerBase
    {
        private readonly ICareerPlanService _careerPlanService;
        private readonly ILogger<MyCareerPlanController> _logger;

        public MyCareerPlanController(
            ICareerPlanService careerPlanService,
            ILogger<MyCareerPlanController> logger)
        {
            _careerPlanService = careerPlanService;
            _logger = logger;
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var claimVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claimVal, out userId);
        }

        /// <summary>
        /// Retrieves the personal career plan evaluation for the authenticated user/admin.
        /// Served from cache when available, or evaluated on-demand.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<MyCareerPlanResponse>>> GetMyCareerPlan([FromQuery] bool forceRefresh = false)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<MyCareerPlanResponse>
                {
                    Success = false,
                    Message = "User identifier is missing or invalid in authentication token."
                });
            }

            try
            {
                var response = await _careerPlanService.GetMyCareerPlanAsync(userId, forceRefresh);
                return Ok(new ApiResponse<MyCareerPlanResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting personal career plan for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<MyCareerPlanResponse>
                {
                    Success = false,
                    Message = "An error occurred while loading your career plan."
                });
            }
        }

        /// <summary>
        /// Forces re-evaluation of the personal career plan for the authenticated user/admin, updating the cache.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<MyCareerPlanResponse>>> RefreshMyCareerPlan()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<MyCareerPlanResponse>
                {
                    Success = false,
                    Message = "User identifier is missing or invalid in authentication token."
                });
            }

            try
            {
                var response = await _careerPlanService.GetMyCareerPlanAsync(userId, forceRefresh: true);
                return Ok(new ApiResponse<MyCareerPlanResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing personal career plan for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<MyCareerPlanResponse>
                {
                    Success = false,
                    Message = "An error occurred while recalculating your career plan."
                });
            }
        }

        /// <summary>
        /// Retrieves paginated contract RT tracking (RT1 to RT6 and current RT) for the authenticated user.
        /// </summary>
        [HttpGet("contracts-rt")]
        public async Task<ActionResult<ApiResponse<ContractRtPageDto>>> GetMyContractsRt(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? asOfDate = null)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "User identifier is missing or invalid in authentication token."
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting personal contract RT tracking for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<ContractRtPageDto>
                {
                    Success = false,
                    Message = "An error occurred while loading contract RT tracking."
                });
            }
        }
    }
}
