using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/imports")]
    [Authorize(Roles = "admin,superadmin")]
    public class ImportHistoryController : ControllerBase
    {
        private readonly IImportSessionRepository _sessionRepository;
        private readonly IImportExecutionService _importExecution;

        public ImportHistoryController(
            IImportSessionRepository sessionRepository,
            IImportExecutionService importExecution)
        {
            _sessionRepository = sessionRepository;
            _importExecution = importExecution;
        }

        [HttpGet("history")]
        public async Task<ActionResult<ApiResponse<List<ImportSession>>>> GetHistory()
        {
            var history = await _sessionRepository.GetAllAsync();
            
            // Only return completed or failed sessions, skip "preview" and "ready" if they are old
            var filteredHistory = history
                .Where(s => s.Status == "completed" || s.Status == "completed_with_errors" || s.Status == "undone")
                .ToList();

            return Ok(new ApiResponse<List<ImportSession>>
            {
                Success = true,
                Data = filteredHistory
            });
        }

        [HttpDelete("{id}/undo")]
        public async Task<ActionResult<ApiResponse<string>>> UndoImport(int id)
        {
            var session = await _sessionRepository.GetByIdAsync(id);
            if (session == null)
            {
                return NotFound(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Import session not found"
                });
            }

            if (session.Status == "undone")
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "This import has already been undone"
                });
            }

            if (session.Status != "completed" && session.Status != "completed_with_errors")
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Only completed imports can be undone"
                });
            }

            var success = await _importExecution.UndoImportAsync(id);
            if (!success)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Failed to undo import. Some records might not have been deleted."
                });
            }

            // Update session status
            session.Status = "undone";
            await _sessionRepository.UpdateAsync(session);

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "Import undone successfully. Created records have been deleted."
            });
        }
    }
}
