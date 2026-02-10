using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;
using SalesApp.Attributes;
using System.Security.Claims;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [HasPermission("imports:execute")]
    public class WizardController : ControllerBase
    {
        private readonly IWizardService _wizardService;

        public WizardController(IWizardService wizardService)
        {
            _wizardService = wizardService;
        }

        [HttpPost("step1-upload")]
        public async Task<ActionResult<ApiResponse<ImportPreviewResponse>>> Step1Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<ImportPreviewResponse> { Success = false, Message = "No file uploaded" });
            }

            try
            {
                var userId = GetCurrentUserId();
                var response = await _wizardService.ProcessStep1UploadAsync(file, userId);
                return Ok(new ApiResponse<ImportPreviewResponse> 
                { 
                    Success = true, 
                    Data = response,
                    Message = "File uploaded and processed for Step 1"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ImportPreviewResponse> { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("step1-template/{uploadId}")]
        public async Task<IActionResult> DownloadTemplate(string uploadId)
        {
            try
            {
                var csvBytes = await _wizardService.GenerateUsersTemplateAsync(uploadId);
                return File(csvBytes, "text/csv", "users.csv");
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("step2-import")]
        public async Task<ActionResult<ApiResponse<ImportStatusResponse>>> Step2Import([FromForm] string uploadId, IFormFile usersFile)
        {
            if (usersFile == null || usersFile.Length == 0)
            {
                return BadRequest(new ApiResponse<ImportStatusResponse> { Success = false, Message = "No users file uploaded" });
            }

            try
            {
                var userId = GetCurrentUserId();
                var response = await _wizardService.ProcessStep2ImportAsync(uploadId, usersFile, userId);
                return Ok(new ApiResponse<ImportStatusResponse> 
                { 
                    Success = true, 
                    Data = response,
                    Message = "Users and matriculas imported successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ImportStatusResponse> { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("step3-contracts/{uploadId}")]
        public async Task<IActionResult> DownloadContracts(string uploadId)
        {
            try
            {
                var csvBytes = await _wizardService.GenerateEnrichedContractsAsync(uploadId);
                return File(csvBytes, "text/csv", "contracts.csv");
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
