using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.ReportViews.DTOs;
using SalesApp.ReportViews.Services;

namespace SalesApp.ReportViews.Controllers
{
    [ApiController]
    [Route("api/report-views")]
    [Authorize]
    public class ReportViewsController : ControllerBase
    {
        private readonly IReportViewService _service;

        public ReportViewsController(IReportViewService service)
        {
            _service = service;
        }

        // ── GET /api/report-views ─────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.ListAsync(callerId);
            return Ok(new { success = true, data = result.Data });
        }

        // ── GET /api/report-views/{viewId} ────────────────────────────────────

        [HttpGet("{viewId}")]
        public async Task<IActionResult> Get(string viewId)
        {
            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.GetAsync(callerId, viewId);
            return MapResult(result);
        }

        // ── POST /api/report-views ────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReportViewRequest request)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.CreateAsync(callerId, request);
            if (!result.Success && result.StatusCode == 400)
                return BadRequest(BuildErrorBody(result.Errors!));

            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        }

        // ── PUT /api/report-views/{viewId} ────────────────────────────────────

        [HttpPut("{viewId}")]
        public async Task<IActionResult> Update(string viewId, [FromBody] UpdateReportViewRequest request)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.UpdateAsync(callerId, viewId, request);
            return MapResult(result);
        }

        // ── DELETE /api/report-views/{viewId} ─────────────────────────────────

        [HttpDelete("{viewId}")]
        public async Task<IActionResult> Delete(string viewId)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.DeleteAsync(callerId, viewId);
            return MapResult(result);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private string? GetCallerId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private bool IsSuperAdmin() =>
            User.IsInRole("superadmin") ||
            User.HasClaim("perm", "system:superadmin");

        private static object BuildErrorBody(List<string> errors) =>
            new
            {
                errors = errors.Select(msg => new { message = msg })
            };

        private IActionResult MapResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
                return Ok(new { success = true, data = result.Data });

            return result.StatusCode switch
            {
                400 => BadRequest(BuildErrorBody(result.Errors ?? new())),
                403 => Forbid(),
                404 => NotFound(new { success = false, message = "View not found." }),
                _   => StatusCode(result.StatusCode, new { success = false })
            };
        }
    }
}
