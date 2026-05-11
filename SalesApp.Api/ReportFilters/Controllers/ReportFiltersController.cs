using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.ReportFilters.DTOs;
using SalesApp.ReportFilters.Services;
using SalesApp.ReportFilters.Validators;
using System.Security.Claims;

namespace SalesApp.ReportFilters.Controllers
{
    /// <summary>
    /// API surface for saved report filters.
    ///
    /// Auth mechanism:
    ///   - All endpoints require [Authorize] (valid JWT).
    ///   - Write endpoints additionally verify the "superadmin" role via ClaimTypes.Role.
    ///   - The superadmin role is identified by ClaimTypes.Role == "superadmin" in the JWT
    ///     (emitted by JwtService when the user's Role.Name == "superadmin", RoleId == 1).
    ///
    /// Business rules are enforced entirely in IReportFilterService —
    /// this controller only parses inputs, calls the service, and maps to HTTP responses.
    /// </summary>
    [ApiController]
    [Route("api/report-filters")]
    [Authorize]
    public class ReportFiltersController : ControllerBase
    {
        private readonly IReportFilterService _service;

        public ReportFiltersController(IReportFilterService service)
        {
            _service = service;
        }

        // ── GET /api/report-filters ───────────────────────────────────────────

        /// <summary>
        /// Returns all shared reports + the caller's own private reports.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.ListAsync(callerId);
            return Ok(new { success = true, data = result.Data });
        }

        // ── GET /api/report-filters/columns/available ─────────────────────────

        /// <summary>
        /// Returns the full list of selectable columns grouped by source entity.
        /// Superadmin only.
        /// </summary>
        [HttpGet("columns/available")]
        public IActionResult GetAvailableColumns()
        {
            if (!IsSuperAdmin()) return Forbid();

            var columns = _service.GetAvailableColumns();
            return Ok(new { success = true, data = columns });
        }

        // ── GET /api/report-filters/{filterId} ────────────────────────────────

        /// <summary>
        /// Returns a single report. Returns 404 if private and not owned by caller.
        /// </summary>
        [HttpGet("{filterId}")]
        public async Task<IActionResult> Get(string filterId)
        {
            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.GetAsync(callerId, filterId);
            return MapResult(result);
        }

        // ── POST /api/report-filters ──────────────────────────────────────────

        /// <summary>Creates a new saved report. Superadmin only.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReportFilterRequest request)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.CreateAsync(callerId, request);
            if (!result.Success && result.StatusCode == 400)
                return BadRequest(BuildErrorBody(result.Errors!));

            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        }

        // ── PUT /api/report-filters/{filterId} ────────────────────────────────

        /// <summary>Replaces filters and columns of an existing report. Superadmin + owner only.</summary>
        [HttpPut("{filterId}")]
        public async Task<IActionResult> Update(string filterId, [FromBody] UpdateReportFilterRequest request)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.UpdateAsync(callerId, filterId, request);
            return MapResult(result);
        }

        // ── DELETE /api/report-filters/{filterId} ─────────────────────────────

        /// <summary>Deletes a saved report. Superadmin + owner only.</summary>
        [HttpDelete("{filterId}")]
        public async Task<IActionResult> Delete(string filterId)
        {
            if (!IsSuperAdmin()) return Forbid();

            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var result = await _service.DeleteAsync(callerId, filterId);
            return MapResult(result);
        }

        // ── GET /api/report-filters/{filterId}/results ────────────────────────

        /// <summary>
        /// Executes the saved report and returns paginated, projected Contract results.
        /// currentUserAsParent is resolved at query time from the authenticated caller's identity.
        /// </summary>
        [HttpGet("{filterId}/results")]
        public async Task<IActionResult> GetResults(
            string filterId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var callerId = GetCallerId();
            if (callerId == null) return Unauthorized();

            var currentUserId = GetCurrentUserId();

            var result = await _service.ExecuteAsync(callerId, filterId, currentUserId, page, pageSize);
            return MapResult(result);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private string? GetCallerId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(raw, out var guid) ? guid : null;
        }

        private bool IsSuperAdmin() =>
            User.IsInRole("superadmin") ||
            User.HasClaim("perm", "system:superadmin");

        private static object BuildErrorBody(List<ValidationError> errors) =>
            new
            {
                errors = errors.Select(e => new { field = e.Field, message = e.Message })
            };

        private IActionResult MapResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
                return Ok(new { success = true, data = result.Data });

            return result.StatusCode switch
            {
                400 => BadRequest(BuildErrorBody(result.Errors ?? new())),
                403 => Forbid(),
                404 => NotFound(new { success = false, message = "Report not found." }),
                _   => StatusCode(result.StatusCode, new { success = false })
            };
        }
    }
}
