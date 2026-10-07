using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Helpdesk.DTOs;
using SalesApp.Helpdesk.Services;

namespace SalesApp.Helpdesk.Controllers
{
    [ApiController]
    [Route("api/helpdesk")]
    [Authorize]
    public class HelpdeskController : ControllerBase
    {
        private readonly IHelpdeskService _service;

        public HelpdeskController(IHelpdeskService service)
        {
            _service = service;
        }

        [HttpPost("questions")]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateHelpdeskQuestionRequest request)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid("Apenas administradores podem enviar perguntas.");

            var userId = GetCallerId();
            var email = GetCallerEmail();
            var name = GetCallerName();

            try
            {
                var result = await _service.CreateQuestionAsync(userId, email, name, request);
                return CreatedAtAction(nameof(GetQuestionById), new { id = result.QuestionId }, new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("questions")]
        public async Task<IActionResult> ListQuestions([FromQuery] string? status = null)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid();

            var isSuperAdmin = callerRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);
            var userId = GetCallerId();

            var list = await _service.ListQuestionsAsync(userId, isSuperAdmin, status);
            return Ok(new { success = true, data = list });
        }

        [HttpGet("questions/{id}")]
        public async Task<IActionResult> GetQuestionById(string id)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid();

            var isSuperAdmin = callerRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);
            var userId = GetCallerId();

            try
            {
                var result = await _service.GetQuestionByIdAsync(id, userId, isSuperAdmin);
                if (result == null) return NotFound(new { success = false, message = "Pergunta não encontrada." });
                return Ok(new { success = true, data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("questions/{id}")]
        public async Task<IActionResult> UpdateQuestion(string id, [FromBody] UpdateHelpdeskQuestionRequest request)
        {
            var userId = GetCallerId();
            try
            {
                var result = await _service.UpdateQuestionContentAsync(id, userId, request);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Pergunta não encontrada." });
            }
        }

        [HttpPatch("questions/{id}/status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateHelpdeskStatusRequest request)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid();

            var isSuperAdmin = callerRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);
            var userId = GetCallerId();

            try
            {
                var result = await _service.UpdateStatusAsync(id, userId, isSuperAdmin, request);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Pergunta não encontrada." });
            }
        }

        [HttpPost("questions/{id}/messages")]
        public async Task<IActionResult> AddMessage(string id, [FromBody] AddHelpdeskMessageRequest request)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid();

            var userId = GetCallerId();
            var name = GetCallerName();

            try
            {
                var result = await _service.AddMessageAsync(id, userId, name, callerRole, request);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Pergunta não encontrada." });
            }
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Ok(new { success = true, data = new { unreadCount = 0 } });

            var isSuperAdmin = callerRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);
            var userId = GetCallerId();

            var result = await _service.GetUnreadCountAsync(userId, isSuperAdmin);
            return Ok(new { success = true, data = result });
        }

        private static bool IsAdminOrSuperAdmin(string role)
        {
            var r = role.ToLowerInvariant();
            return r == "admin" || r == "superadmin";
        }

        private string GetCallerId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("userId")?.Value
            ?? string.Empty;

        private string GetCallerRole() =>
            User.FindFirst(ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value
            ?? string.Empty;

        private string GetCallerEmail() =>
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? string.Empty;

        private string GetCallerName() =>
            User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("name")?.Value
            ?? GetCallerEmail();
    }
}
