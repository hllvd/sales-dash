using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;
using SalesApp.Utils;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IMessageService _messageService;
        
        public GroupsController(IGroupRepository groupRepository, IMessageService messageService)
        {
            _groupRepository = groupRepository;
            _messageService = messageService;
        }
        
        [HttpGet]
        [HasPermission("groups:read")]
        public async Task<ActionResult<ApiResponse<List<GroupResponse>>>> GetGroups([FromQuery] bool includeInactive = false)
        {
            var groups = await _groupRepository.GetAllAsync(includeInactive);
            
            return Ok(new ApiResponse<List<GroupResponse>>
            {
                Success = true,
                Data = groups.Select(MapToGroupResponse).ToList(),
                Message = _messageService.Get(AppMessage.GroupsRetrievedSuccessfully)
            });
        }

        [HttpGet("export-xlsx")]
        [HasPermission("groups:read")]
        public async Task<IActionResult> ExportGroupsXlsx(
            [FromQuery] string? status = "active",
            [FromQuery] string? search = null)
        {
            if (!GroupTableHelper.TryParseStatusFilter(status, out var statusFilter))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Filtro de status inválido. Valores aceitos: active, inactive, all."
                });
            }

            var groups = await _groupRepository.GetAllAsync(includeInactive: true);
            var filteredAndSorted = GroupTableHelper.FilterAndSortGroups(groups, statusFilter, search);
            var content = GroupTableHelper.BuildGroupsXlsx(filteredAndSorted);

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "grupos.xlsx"
            );
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> GetGroup(int id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
            {
                return NotFound(new ApiResponse<GroupResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.GroupNotFound)
                });
            }
            
            return Ok(new ApiResponse<GroupResponse>
            {
                Success = true,
                Data = MapToGroupResponse(group),
                Message = _messageService.Get(AppMessage.GroupRetrievedSuccessfully)
            });
        }
        
        [HttpPost]
        [HasPermission("groups:write")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> CreateGroup(GroupRequest request)
        {
            request.Name = NormalizationUtils.NormalizeNumber(request.Name);
            if (await _groupRepository.NameExistsAsync(request.Name))
            {
                return BadRequest(new ApiResponse<GroupResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.GroupNameAlreadyExists)
                });
            }
            
            var group = new Group
            {
                Name = request.Name,
                Description = request.Description,
                Commission = request.Commission,
                Segment = request.Segment
            };
            
            await _groupRepository.CreateAsync(group);
            
            return Ok(new ApiResponse<GroupResponse>
            {
                Success = true,
                Data = MapToGroupResponse(group),
                Message = _messageService.Get(AppMessage.GroupCreatedSuccessfully)
            });
        }
        
        [HttpPut("{id}")]
        [HasPermission("groups:write")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> UpdateGroup(int id, UpdateGroupRequest request)
        {
            request.Name = NormalizationUtils.NormalizeNumber(request.Name);
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
            {
                return NotFound(new ApiResponse<GroupResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.GroupNotFound)
                });
            }
            
            if (!string.IsNullOrEmpty(request.Name))
            {
                if (await _groupRepository.NameExistsAsync(request.Name, id))
                {
                    return BadRequest(new ApiResponse<GroupResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.GroupNameAlreadyExists)
                    });
                }
                group.Name = request.Name;
            }
            
            if (request.Description != null)
                group.Description = request.Description;
                
            if (request.Commission.HasValue)
                group.Commission = request.Commission.Value;
                
            if (request.Segment.HasValue)
                group.Segment = request.Segment.Value;
                
            if (request.IsActive.HasValue)
                group.IsActive = request.IsActive.Value;
            
            await _groupRepository.UpdateAsync(group);
            
            return Ok(new ApiResponse<GroupResponse>
            {
                Success = true,
                Data = MapToGroupResponse(group),
                Message = _messageService.Get(AppMessage.GroupUpdatedSuccessfully)
            });
        }
        
        [HttpDelete("{id}")]
        [HasPermission("groups:write")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteGroup(int id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || !group.IsActive)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.GroupNotFound)
                });
            }
            
            group.IsActive = false;
            await _groupRepository.UpdateAsync(group);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.GroupDeletedSuccessfully)
            });
        }
        
        [HttpPost("import")]
        [HasPermission("groups:write")]
        public async Task<ActionResult<ApiResponse<GroupImportResponse>>> ImportGroups([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<GroupImportResponse>
                {
                    Success = false,
                    Message = "Nenhum arquivo enviado."
                });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new ApiResponse<GroupImportResponse>
                {
                    Success = false,
                    Message = "Formato de arquivo inválido. Por favor, envie um arquivo .xlsx."
                });
            }

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");

            var response = new GroupImportResponse();

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.Dimension == null)
            {
                return BadRequest(new ApiResponse<GroupImportResponse>
                {
                    Success = false,
                    Message = "A planilha enviada está vazia ou não contém dados."
                });
            }

            // Find header row and columns
            int headerRow = 1;
            int groupCol = -1;
            int segmentCol = -1;

            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var val = worksheet.Cells[headerRow, col].Text?.Trim().ToLowerInvariant() ?? "";
                var cleanVal = val
                    .Replace("ç", "c")
                    .Replace("ã", "a")
                    .Replace("õ", "o")
                    .Replace("é", "e")
                    .Replace("ê", "e")
                    .Replace("í", "i")
                    .Replace("ó", "o")
                    .Replace("ú", "u");

                if (groupCol == -1 && (cleanVal == "grupo" || cleanVal == "groupo" || cleanVal == "nome" || cleanVal == "group" || cleanVal == "name"))
                {
                    groupCol = col;
                }
                else if (segmentCol == -1 && (cleanVal == "segmento" || cleanVal == "segment" || cleanVal == "seguimento"))
                {
                    segmentCol = col;
                }
            }

            if (groupCol == -1)
            {
                return BadRequest(new ApiResponse<GroupImportResponse>
                {
                    Success = false,
                    Message = "Coluna 'Grupo' não foi encontrada na planilha. Verifique o cabeçalho."
                });
            }

            if (segmentCol == -1)
            {
                return BadRequest(new ApiResponse<GroupImportResponse>
                {
                    Success = false,
                    Message = "Coluna 'Segmento' não foi encontrada na planilha. Verifique o cabeçalho."
                });
            }

            for (int row = headerRow + 1; row <= worksheet.Dimension.End.Row; row++)
            {
                var rawGroup = worksheet.Cells[row, groupCol].Text?.Trim();
                var rawSegment = worksheet.Cells[row, segmentCol].Text?.Trim();

                // Skip completely empty rows
                if (string.IsNullOrWhiteSpace(rawGroup) && string.IsNullOrWhiteSpace(rawSegment))
                {
                    continue;
                }

                var normalizedGroup = NormalizationUtils.NormalizeNumber(rawGroup);
                if (string.IsNullOrWhiteSpace(normalizedGroup))
                {
                    response.Errors.Add(new GroupImportRowError
                    {
                        RowNumber = row,
                        GroupName = rawGroup,
                        Error = "Nome do grupo é obrigatório."
                    });
                    continue;
                }

                if (!SegmentExtensions.TryParseSegment(rawSegment, out var segmentValue))
                {
                    response.Errors.Add(new GroupImportRowError
                    {
                        RowNumber = row,
                        GroupName = normalizedGroup,
                        Error = $"Segmento '{rawSegment}' inválido. Valores aceitos: Lar, Motores, Serviços ou vazio."
                    });
                    continue;
                }

                response.TotalRows++;

                var existingGroup = await _groupRepository.GetByNameAsync(normalizedGroup);
                if (existingGroup != null)
                {
                    existingGroup.Segment = segmentValue;
                    if (!existingGroup.IsActive)
                    {
                        existingGroup.IsActive = true;
                    }
                    await _groupRepository.UpdateAsync(existingGroup);
                    response.UpdatedCount++;
                }
                else
                {
                    var newGroup = new Group
                    {
                        Name = normalizedGroup,
                        Segment = segmentValue,
                        Commission = 0,
                        IsActive = true
                    };
                    await _groupRepository.CreateAsync(newGroup);
                    response.CreatedCount++;
                }
            }

            return Ok(new ApiResponse<GroupImportResponse>
            {
                Success = true,
                Data = response,
                Message = $"Importação concluída: {response.CreatedCount} criados, {response.UpdatedCount} atualizados, {response.ErrorCount} erros."
            });
        }

        [HttpGet("import/template")]
        [HasPermission("groups:read")]
        public IActionResult DownloadImportTemplate()
        {
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Grupos");

            worksheet.Cells[1, 1].Value = "Grupo";
            worksheet.Cells[1, 2].Value = "Segmento";

            using (var range = worksheet.Cells[1, 1, 1, 2])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(209, 213, 219));
            }

            worksheet.Cells[2, 1].Value = "1010";
            worksheet.Cells[2, 2].Value = "Lar";

            worksheet.Cells[3, 1].Value = "1020";
            worksheet.Cells[3, 2].Value = "Motores";

            worksheet.Cells[4, 1].Value = "1030";
            worksheet.Cells[4, 2].Value = "Serviços";

            worksheet.Cells[5, 1].Value = "1040";
            worksheet.Cells[5, 2].Value = "";

            worksheet.Column(1).AutoFit();
            worksheet.Column(2).AutoFit();

            var content = package.GetAsByteArray();
            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "modelo_importacao_grupos.xlsx"
            );
        }
        
        private GroupResponse MapToGroupResponse(Group group)
        {
            return new GroupResponse
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Commission = group.Commission,
                Segment = group.Segment,
                SegmentName = SegmentExtensions.ToDisplayName(group.Segment),
                IsActive = group.IsActive,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }
    }
}