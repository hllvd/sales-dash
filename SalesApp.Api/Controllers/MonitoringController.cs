using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Services;
using SalesApp.Attributes;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
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

        [HttpGet("licensing")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<LicensingReportResponse>>> GetLicensingReport(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int? minimumDays)
        {
            if (year <= 0 || month < 1 || month > 12)
            {
                return BadRequest(new ApiResponse<LicensingReportResponse>
                {
                    Success = false,
                    Message = "Invalid year or month parameters"
                });
            }

            var report = await _monitoringService.GetLicensingReportAsync(year, month, minimumDays);
            return Ok(new ApiResponse<LicensingReportResponse>
            {
                Success = true,
                Data = report,
                Message = "Licensing report generated successfully"
            });
        }

        [HttpGet("licensing/export-xlsx")]
        [HasPermission("system:superadmin")]
        public async Task<IActionResult> ExportLicensingXlsx(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int? minimumDays)
        {
            if (year <= 0 || month < 1 || month > 12)
            {
                return BadRequest("Parâmetros de ano ou mês inválidos.");
            }

            var report = await _monitoringService.GetLicensingReportAsync(year, month, minimumDays);
            if (report == null)
            {
                return NotFound("Relatório de licenciamento não encontrado.");
            }

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add($"Licenciamento {month:D2}-{year}");

            var headers = new[] { "Nome", "Email", "Cargo", "Equipe", "Dias Ativos no Mês", "Status Licenciamento" };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }

            if (report.Users != null)
            {
                for (int r = 0; r < report.Users.Count; r++)
                {
                    var u = report.Users[r];
                    worksheet.Cells[r + 2, 1].Value = u.Name;
                    worksheet.Cells[r + 2, 2].Value = u.Email;
                    worksheet.Cells[r + 2, 3].Value = u.Role;
                    worksheet.Cells[r + 2, 4].Value = u.TeamName;
                    worksheet.Cells[r + 2, 5].Value = $"{u.ActiveDaysInMonth} dias";
                    worksheet.Cells[r + 2, 6].Value = u.IsLicensed ? "Licenciado" : "Abaixo do mínimo";
                }
            }

            worksheet.Cells.AutoFitColumns();
            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"licenciamento-{month}-{year}.xlsx");
        }
    }
}
