using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/career-plan")]
    [Authorize(Roles = "SuperAdmin,superadmin")]
    public class CareerPlanController : ControllerBase
    {
        private readonly ICareerPlanService _careerPlanService;
        private readonly ILogger<CareerPlanController> _logger;

        public CareerPlanController(
            ICareerPlanService careerPlanService,
            ILogger<CareerPlanController> logger)
        {
            _careerPlanService = careerPlanService;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates career plan progress and metrics for a specific user.
        /// Available strictly to SuperAdmins.
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<ActionResult<ApiResponse<CareerPlanEvaluationResponse>>> EvaluateUser(
            Guid userId,
            [FromQuery] DateTime? asOfDate = null)
        {
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
        /// Returns summary of loaded career plan configuration.
        /// Available strictly to SuperAdmins.
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
    }
}
