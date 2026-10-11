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

        [HttpPost("questions/upload-image")]
        public async Task<IActionResult> UploadImage([FromForm] Microsoft.AspNetCore.Http.IFormFile file)
        {
            var callerRole = GetCallerRole();
            if (!IsAdminOrSuperAdmin(callerRole))
                return Forbid("Apenas administradores podem fazer upload de imagens.");

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Nenhum arquivo enviado." });

            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new { success = false, message = "Apenas arquivos de imagem (PNG, JPG, GIF, WebP) são permitidos. Vídeos e outros arquivos não são aceitos." });
            }

            // Max 10MB
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "A imagem deve ter no máximo 10MB." });
            }

            using var memoryStream = new System.IO.MemoryStream();
            await file.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            var base64 = Convert.ToBase64String(bytes);
            var dataUri = $"data:{file.ContentType.ToLowerInvariant()};base64,{base64}";

            return Ok(new { success = true, data = new { imageUrl = dataUri } });
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

        [HttpGet("knowledge-base")]
        public async Task<IActionResult> ListKnowledgeBaseArticles([FromQuery] string? category = null)
        {
            var articles = await _service.ListKnowledgeBaseArticlesAsync(category);
            return Ok(new { success = true, data = articles });
        }

        [HttpGet("knowledge-base/{slug}")]
        public async Task<IActionResult> GetKnowledgeBaseArticle(string slug)
        {
            var article = await _service.GetKnowledgeBaseArticleBySlugAsync(slug);
            if (article == null)
                return NotFound(new { success = false, message = "Artigo não encontrado." });

            return Ok(new { success = true, data = article });
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
