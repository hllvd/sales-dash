using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;
using SalesApp.Attributes;
using SalesApp.Repositories;
using System.Security.Claims;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [HasPermission("imports:execute")]
    public class WizardController : ControllerBase
    {
        private readonly IWizardService _wizardService;
        private readonly IUserRepository _userRepository;

        public WizardController(IWizardService wizardService, IUserRepository userRepository)
        {
            _wizardService = wizardService;
            _userRepository = userRepository;
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
                
                // Validate user exists (protects against stale tokens after DB resets)
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized(new ApiResponse<ImportPreviewResponse> { Success = false, Message = "Your session is invalid. Please log in again." });
                }

                var response = await _wizardService.ProcessStep1UploadAsync(file, userId);
                
                // Diagnostic logging
                Console.WriteLine($"[Wizard] Step 1: File '{file.FileName}' uploaded by {userId}.");
                Console.WriteLine($"[Wizard] Step 1: Detected {response.TotalRows} rows. Match: {response.IsTemplateMatch}. Message: {response.MatchMessage}");
                
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
                Console.WriteLine($"[Wizard] Requesting users.xlsx template for session: {uploadId}");
                var xlsxBytes = await _wizardService.GenerateUsersTemplateAsync(uploadId);
                Console.WriteLine($"[Wizard] Successfully generated users.xlsx ({xlsxBytes.Length} bytes) for session: {uploadId}");
                return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users.xlsx");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Wizard] ERROR generating template for {uploadId}: {ex.Message}");
                if (ex.InnerException != null) 
                    Console.WriteLine($"[Wizard] INNER ERROR: {ex.InnerException.Message}");
                    
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

                // Validate user exists (protects against stale tokens after DB resets)
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized(new ApiResponse<ImportStatusResponse> { Success = false, Message = "Your session is invalid. Please log in again." });
                }

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
                var xlsxBytes = await _wizardService.GenerateEnrichedContractsAsync(uploadId);
                return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "contracts.xlsx");
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
