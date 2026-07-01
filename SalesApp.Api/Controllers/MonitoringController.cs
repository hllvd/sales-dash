using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;
using SalesApp.Attributes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MonitoringController : ControllerBase
    {
        private readonly IMonitoringService _monitoringService;

        public MonitoringController(IMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        [HttpGet("contracts")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<MatriculaHealthResponse>>>> GetMatriculaHealth()
        {
            var healthData = await _monitoringService.GetMatriculaHealthAsync();
            return Ok(new ApiResponse<List<MatriculaHealthResponse>>
            {
                Success = true,
                Data = healthData,
                Message = "Matricula health data retrieved successfully"
            });
        }

        [HttpGet("equipes")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<TeamMatriculaHealthResponse>>>> GetEquipesHealth()
        {
            var teamsHealthData = await _monitoringService.GetEquipesHealthAsync();
            return Ok(new ApiResponse<List<TeamMatriculaHealthResponse>>
            {
                Success = true,
                Data = teamsHealthData,
                Message = "Equipes health data retrieved successfully"
            });
        }

        [HttpGet("admins")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<AdminImportStatsResponse>>>> GetAdminImportStats()
        {
            var adminStats = await _monitoringService.GetAdminImportStatsAsync();
            return Ok(new ApiResponse<List<AdminImportStatsResponse>>
            {
                Success = true,
                Data = adminStats,
                Message = "Admin import statistics retrieved successfully"
            });
        }
    }
}
