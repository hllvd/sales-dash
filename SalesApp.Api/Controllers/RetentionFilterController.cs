using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RetentionFilterController : ControllerBase
    {
        private readonly IRetentionFilterService _filterService;

        public RetentionFilterController(IRetentionFilterService filterService)
        {
            _filterService = filterService;
        }

        private bool IsSuperAdmin()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            return emailClaim == "superadmin@salesapp.com" ||
                   emailClaim == "superadmin@test.com" ||
                   User.HasClaim("perm", "system:superadmin");
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<RetentionFilterProcessResponse>>> PreviewFilter(
            [FromForm] IFormFile fileA,
            [FromForm] IFormFile fileB)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (fileA == null || fileA.Length == 0)
            {
                return BadRequest(new ApiResponse<RetentionFilterProcessResponse>
                {
                    Success = false,
                    Message = "O arquivo do Modelo A (Base de Retenção) é obrigatório."
                });
            }

            if (fileB == null || fileB.Length == 0)
            {
                return BadRequest(new ApiResponse<RetentionFilterProcessResponse>
                {
                    Success = false,
                    Message = "O arquivo do Modelo B (Lista de Contratos) é obrigatório."
                });
            }

            try
            {
                using var streamA = fileA.OpenReadStream();
                using var streamB = fileB.OpenReadStream();

                var result = await _filterService.ProcessFilterPreviewAsync(
                    streamA, fileA.FileName,
                    streamB, fileB.FileName);

                return Ok(new ApiResponse<RetentionFilterProcessResponse>
                {
                    Success = true,
                    Data = result,
                    Message = $"Filtro processado com sucesso: {result.Stats.MatchedRowsModelC} registros retidos de {result.Stats.TotalRowsModelA} totais."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<RetentionFilterProcessResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<RetentionFilterProcessResponse>
                {
                    Success = false,
                    Message = $"Erro ao processar filtro: {ex.Message}"
                });
            }
        }

        [HttpPost("download")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> DownloadFilteredFile(
            [FromForm] IFormFile fileA,
            [FromForm] IFormFile fileB)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (fileA == null || fileA.Length == 0)
            {
                return BadRequest("O arquivo do Modelo A (Base de Retenção) é obrigatório.");
            }

            if (fileB == null || fileB.Length == 0)
            {
                return BadRequest("O arquivo do Modelo B (Lista de Contratos) é obrigatório.");
            }

            try
            {
                using var streamA = fileA.OpenReadStream();
                using var streamB = fileB.OpenReadStream();

                var exportResult = await _filterService.FilterAndGenerateWorkbookAsync(
                    streamA, fileA.FileName,
                    streamB, fileB.FileName);

                Response.Headers.Append("X-Total-Rows-A", exportResult.Stats.TotalRowsModelA.ToString());
                Response.Headers.Append("X-Total-Contracts-B", exportResult.Stats.TotalContractsModelB.ToString());
                Response.Headers.Append("X-Matched-Rows-C", exportResult.Stats.MatchedRowsModelC.ToString());
                Response.Headers.Append("X-Removed-Rows", exportResult.Stats.RemovedRows.ToString());
                Response.Headers.Append("Access-Control-Expose-Headers", "X-Total-Rows-A, X-Total-Contracts-B, X-Matched-Rows-C, X-Removed-Rows, Content-Disposition");

                return File(exportResult.FileBytes, exportResult.ContentType, exportResult.FileName);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao gerar arquivo filtrado: {ex.Message}");
            }
        }
    }
}
