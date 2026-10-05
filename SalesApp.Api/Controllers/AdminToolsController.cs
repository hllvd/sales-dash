using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Utils;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/admin-tools")]
    [Authorize]
    public class AdminToolsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminToolsController(AppDbContext context)
        {
            _context = context;
        }

        private bool IsSuperAdmin()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            return emailClaim == "superadmin@salesapp.com" ||
                   emailClaim == "superadmin@test.com" ||
                   User.HasClaim("perm", "system:superadmin");
        }

        [HttpGet("users/search")]
        public async Task<ActionResult<ApiResponse<List<AdminUserSearchItem>>>> SearchUsers([FromQuery] string? query)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            var usersQuery = _context.Users
                .AsNoTracking()
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                usersQuery = usersQuery.Where(u => u.Email.ToLower().Contains(q) || u.Name.ToLower().Contains(q));
            }

            var users = await usersQuery
                .OrderBy(u => u.Name)
                .Take(20)
                .Select(u => new AdminUserSearchItem
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    Matriculas = u.UserMatriculas
                        .Where(um => um.IsActive && um.Matricula != null)
                        .Select(um => um.Matricula.MatriculaNumber)
                        .ToList()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<AdminUserSearchItem>>
            {
                Success = true,
                Data = users,
                Message = "Usuários consultados com sucesso."
            });
        }

        [HttpPost("migrate-contracts")]
        public async Task<ActionResult<ApiResponse<AdminMigrateContractsResult>>> MigrateContracts([FromBody] AdminMigrateContractsRequest request)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.FromEmail) || string.IsNullOrWhiteSpace(request.ToEmail))
            {
                return BadRequest(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = "Os e-mails de origem e destino são obrigatórios."
                });
            }

            var fromEmail = request.FromEmail.Trim().ToLower();
            var toEmail = request.ToEmail.Trim().ToLower();

            if (fromEmail == toEmail)
            {
                return BadRequest(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = "O e-mail de origem e destino devem ser diferentes."
                });
            }

            var fromUser = await _context.Users
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == fromEmail);

            if (fromUser == null)
            {
                return NotFound(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Usuário de origem não encontrado: {request.FromEmail}"
                });
            }

            var toUser = await _context.Users
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == toEmail);

            if (toUser == null)
            {
                return NotFound(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Usuário de destino não encontrado: {request.ToEmail}"
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Fetch all contracts for the 'from' user
                var contracts = await _context.Contracts
                    .Where(c => c.UserInternalId == fromUser.InternalId)
                    .ToListAsync();

                int matriculasMigrated = 0;

                if (request.MigrateMatricula)
                {
                    var fromMatriculas = await _context.UserMatriculas
                        .Where(um => um.UserInternalId == fromUser.InternalId)
                        .ToListAsync();

                    var toMatriculas = await _context.UserMatriculas
                        .Where(um => um.UserInternalId == toUser.InternalId)
                        .ToListAsync();

                    matriculasMigrated = fromMatriculas.Count;

                    foreach (var um in fromMatriculas)
                    {
                        var existingToUm = toMatriculas.FirstOrDefault(m => m.MatriculaId == um.MatriculaId);
                        if (existingToUm != null)
                        {
                            if (um.IsOwner)
                            {
                                existingToUm.IsOwner = true;
                                existingToUm.UpdatedAt = DateTime.UtcNow;
                            }
                            _context.UserMatriculas.Remove(um);
                        }
                        else
                        {
                            um.UserInternalId = toUser.InternalId;
                            um.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                foreach (var contract in contracts)
                {
                    contract.UserInternalId = toUser.InternalId;
                    contract.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = true,
                    Data = new AdminMigrateContractsResult
                    {
                        ContractsMigrated = contracts.Count,
                        MatriculasMigrated = matriculasMigrated,
                        FromUser = $"{fromUser.Name} ({fromUser.Email})",
                        ToUser = $"{toUser.Name} ({toUser.Email})"
                    },
                    Message = $"{contracts.Count} contratos migrados com sucesso para {toUser.Name}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Erro ao realizar migração de contratos: {ex.Message}"
                });
            }
        }

        private async Task<List<AdminTeamInconsistencyItemDto>> ComputeTeamInconsistenciesAsync()
        {
            var teams = await _context.Teams
                .AsNoTracking()
                .ToListAsync();

            var teamById = teams.ToDictionary(t => t.Id);
            var ownedTeamByUserInternalId = teams
                .Where(t => t.OwnerUserInternalId.HasValue)
                .GroupBy(t => t.OwnerUserInternalId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var users = await _context.Users
                .AsNoTracking()
                .Include(u => u.ParentUser)
                .OrderBy(u => u.Name)
                .ToListAsync();

            var contractStats = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.UserInternalId.HasValue && c.IsActive)
                .GroupBy(c => c.UserInternalId!.Value)
                .Select(g => new
                {
                    UserInternalId = g.Key,
                    Count = g.Count(),
                    EarliestDate = g.Min(c => c.SaleStartDate)
                })
                .ToDictionaryAsync(x => x.UserInternalId, x => (Count: x.Count, EarliestDate: (DateTime?)x.EarliestDate));

            var allUserTeams = await _context.UserTeams
                .AsNoTracking()
                .Include(ut => ut.Team)
                .ToListAsync();

            var userTeamsByUser = allUserTeams
                .GroupBy(ut => ut.UserInternalId)
                .ToDictionary(g => g.Key, g => g.OrderBy(ut => ut.StartDate).ToList());

            // Build parent current team map
            var parentTeamMap = new Dictionary<int, string>();
            foreach (var u in users)
            {
                if (userTeamsByUser.TryGetValue(u.InternalId, out var uTeams))
                {
                    var activeParentTeam = uTeams
                        .Where(ut => ut.Team != null && ut.Team.IsActive && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                        .OrderByDescending(ut => ut.StartDate)
                        .FirstOrDefault();
                    if (activeParentTeam != null)
                    {
                        parentTeamMap[u.InternalId] = activeParentTeam.Team?.Name ?? $"Equipe #{activeParentTeam.TeamId}";
                    }
                }
            }

            var results = new List<AdminTeamInconsistencyItemDto>();

            foreach (var user in users)
            {
                userTeamsByUser.TryGetValue(user.InternalId, out var uTeams);
                uTeams ??= new List<UserTeam>();

                var history = uTeams.Select(ut => new TeamHistoryItemDto
                {
                    UserTeamId = ut.Id,
                    TeamId = ut.TeamId,
                    TeamName = ut.Team?.Name ?? $"Equipe #{ut.TeamId}",
                    TeamIsActive = ut.Team?.IsActive ?? true,
                    StartDate = ut.StartDate,
                    EndDate = ut.EndDate,
                    IsActivePeriod = ut.EndDate == null || ut.EndDate > DateTime.UtcNow
                }).ToList();

                var activePeriods = history.Where(h => h.IsActivePeriod).ToList();
                var currentActivePeriod = activePeriods
                    .Where(h => h.TeamIsActive)
                    .OrderByDescending(h => h.StartDate)
                    .FirstOrDefault();

                ownedTeamByUserInternalId.TryGetValue(user.InternalId, out var ownedTeam);
                contractStats.TryGetValue(user.InternalId, out var cStats);
                var earliestDate = cStats.EarliestDate;
                var totalContracts = cStats.Count;

                string? parentTeamName = null;
                if (user.ParentUser != null && parentTeamMap.TryGetValue(user.ParentUser.InternalId, out var pTeam))
                {
                    parentTeamName = pTeam;
                }

                var inconsistencies = new List<InconsistencyDescriptorDto>();

                // Inconsistency 1: Owner of team without active membership in that team
                if (ownedTeam != null)
                {
                    bool hasActiveInOwned = activePeriods.Any(p => p.TeamId == ownedTeam.Id);
                    if (!hasActiveInOwned)
                    {
                        inconsistencies.Add(new InconsistencyDescriptorDto
                        {
                            Type = "OwnerWithoutActiveMembership",
                            Description = $"Definido como proprietário da equipe '{ownedTeam.Name}', mas não possui vínculo ativo como membro nesta equipe.",
                            Severity = "error"
                        });
                    }
                }

                // Inconsistency 2: Multiple active memberships simultaneously
                if (activePeriods.Count > 1)
                {
                    var teamNames = string.Join(", ", activePeriods.Select(p => p.TeamName));
                    inconsistencies.Add(new InconsistencyDescriptorDto
                    {
                        Type = "MultipleActiveMemberships",
                        Description = $"Possui {activePeriods.Count} vínculos ativos simultâneos nas equipes: {teamNames}.",
                        Severity = "error"
                    });
                }

                // Inconsistency 3: Active in inactive/deleted team
                var inactiveTeamPeriods = activePeriods.Where(p => !p.TeamIsActive).ToList();
                if (inactiveTeamPeriods.Any())
                {
                    var inactiveNames = string.Join(", ", inactiveTeamPeriods.Select(p => p.TeamName));
                    inconsistencies.Add(new InconsistencyDescriptorDto
                    {
                        Type = "ActiveInInactiveTeam",
                        Description = $"Possui vínculo ativo em equipe(s) desativada(s): {inactiveNames}.",
                        Severity = "warning"
                    });
                }

                // Inconsistency 4: Active user with contracts but no active team currently
                if (user.IsActive && totalContracts > 0 && currentActivePeriod == null)
                {
                    inconsistencies.Add(new InconsistencyDescriptorDto
                    {
                        Type = "ContractsWithoutActiveTeam",
                        Description = $"Usuário ativo com {totalContracts} contrato(s) registrado(s), mas está atualmente sem nenhuma equipe ativa.",
                        Severity = "warning"
                    });
                }

                if (!inconsistencies.Any())
                {
                    continue;
                }

                // Calculate smart recommendation
                MigrationRecommendationDto? recommendation = null;

                if (ownedTeam != null && !activePeriods.Any(p => p.TeamId == ownedTeam.Id))
                {
                    recommendation = new MigrationRecommendationDto
                    {
                        SuggestedTeamId = ownedTeam.Id,
                        SuggestedTeamName = ownedTeam.Name,
                        SuggestedStartDate = earliestDate ?? DateTime.UtcNow.Date,
                        SuggestSetAsOwner = true,
                        Reason = $"Vincular como membro ativo na equipe '{ownedTeam.Name}' (da qual é proprietário) a partir da data de seu 1º contrato."
                    };
                }
                else if (activePeriods.Count > 1)
                {
                    var bestActive = currentActivePeriod ?? activePeriods.OrderByDescending(p => p.StartDate).First();
                    recommendation = new MigrationRecommendationDto
                    {
                        SuggestedTeamId = bestActive.TeamId,
                        SuggestedTeamName = bestActive.TeamName,
                        SuggestedStartDate = bestActive.StartDate,
                        SuggestSetAsOwner = ownedTeam != null && ownedTeam.Id == bestActive.TeamId,
                        Reason = $"Manter como equipe ativa a mais recente ('{bestActive.TeamName}') e encerrar os períodos sobrepostos anteriores."
                    };
                }
                else if (inactiveTeamPeriods.Any())
                {
                    int? suggestedId = null;
                    string? suggestedName = null;
                    if (user.ParentUser != null)
                    {
                        var parentActiveTeam = allUserTeams
                            .Where(ut => ut.UserInternalId == user.ParentUser.InternalId && ut.Team != null && ut.Team.IsActive && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                            .OrderByDescending(ut => ut.StartDate)
                            .FirstOrDefault();
                        if (parentActiveTeam != null)
                        {
                            suggestedId = parentActiveTeam.TeamId;
                            suggestedName = parentActiveTeam.Team?.Name;
                        }
                    }

                    recommendation = new MigrationRecommendationDto
                    {
                        SuggestedTeamId = suggestedId,
                        SuggestedTeamName = suggestedName,
                        SuggestedStartDate = DateTime.UtcNow.Date,
                        SuggestSetAsOwner = false,
                        Reason = suggestedId.HasValue
                            ? $"Encerrar o vínculo na equipe inativa e vincular à equipe do supervisor ('{suggestedName}')."
                            : "Encerrar o vínculo na equipe inativa e escolher uma nova equipe ativa."
                    };
                }
                else if (user.IsActive && totalContracts > 0 && currentActivePeriod == null)
                {
                    int? suggestedId = null;
                    string? suggestedName = null;
                    if (user.ParentUser != null)
                    {
                        var parentActiveTeam = allUserTeams
                            .Where(ut => ut.UserInternalId == user.ParentUser.InternalId && ut.Team != null && ut.Team.IsActive && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                            .OrderByDescending(ut => ut.StartDate)
                            .FirstOrDefault();
                        if (parentActiveTeam != null)
                        {
                            suggestedId = parentActiveTeam.TeamId;
                            suggestedName = parentActiveTeam.Team?.Name;
                        }
                    }

                    recommendation = new MigrationRecommendationDto
                    {
                        SuggestedTeamId = suggestedId,
                        SuggestedTeamName = suggestedName,
                        SuggestedStartDate = earliestDate ?? DateTime.UtcNow.Date,
                        SuggestSetAsOwner = false,
                        Reason = suggestedId.HasValue
                            ? $"Vincular à equipe do supervisor ('{suggestedName}') a partir da data de seu 1º contrato."
                            : "Vincular a uma equipe ativa a partir da data de seu 1º contrato."
                    };
                }

                results.Add(new AdminTeamInconsistencyItemDto
                {
                    UserId = user.Id,
                    UserInternalId = user.InternalId,
                    UserName = user.Name,
                    UserEmail = user.Email,
                    IsActive = user.IsActive,
                    ParentEmail = user.ParentUser?.Email,
                    ParentUserName = user.ParentUser?.Name,
                    ParentTeamName = parentTeamName,
                    CurrentTeamId = currentActivePeriod?.TeamId,
                    CurrentTeamName = currentActivePeriod?.TeamName,
                    OwnedTeamId = ownedTeam?.Id,
                    OwnedTeamName = ownedTeam?.Name,
                    EarliestContractDate = earliestDate,
                    TotalContractsCount = totalContracts,
                    TeamHistory = history,
                    Inconsistencies = inconsistencies,
                    Recommendation = recommendation
                });
            }

            return results;
        }

        [HttpGet("team-inconsistencies")]
        public async Task<ActionResult<ApiResponse<List<AdminTeamInconsistencyItemDto>>>> GetTeamInconsistencies()
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            var items = await ComputeTeamInconsistenciesAsync();

            return Ok(new ApiResponse<List<AdminTeamInconsistencyItemDto>>
            {
                Success = true,
                Data = items,
                Message = $"{items.Count} inconsistência(s) de equipes encontrada(s)."
            });
        }

        [HttpPost("team-inconsistencies/migrate")]
        public async Task<ActionResult<ApiResponse<AdminMigrateTeamMemberResult>>> MigrateTeamMember([FromBody] AdminMigrateTeamMemberRequest request)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest(new ApiResponse<AdminMigrateTeamMemberResult>
                {
                    Success = false,
                    Message = "Dados de requisição inválidos."
                });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound(new ApiResponse<AdminMigrateTeamMemberResult>
                {
                    Success = false,
                    Message = "Usuário não encontrado."
                });
            }

            var targetTeam = await _context.Teams.FirstOrDefaultAsync(t => t.Id == request.TargetTeamId);
            if (targetTeam == null)
            {
                return NotFound(new ApiResponse<AdminMigrateTeamMemberResult>
                {
                    Success = false,
                    Message = "Equipe de destino não encontrada."
                });
            }

            var startDate = request.StartDate.Date;
            var endDate = request.EndDate.HasValue ? request.EndDate.Value.Date : (DateTime?)null;

            if (endDate.HasValue && startDate > endDate.Value)
            {
                return BadRequest(new ApiResponse<AdminMigrateTeamMemberResult>
                {
                    Success = false,
                    Message = "A data de início deve ser anterior ou igual à data de término."
                });
            }

            int closedCount = 0;
            if (request.CloseConflictingPeriods)
            {
                var existingUserTeams = await _context.UserTeams
                    .Where(ut => ut.UserInternalId == user.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                    .ToListAsync();

                foreach (var ut in existingUserTeams)
                {
                    if (ut.TeamId == targetTeam.Id && ut.StartDate == startDate)
                    {
                        continue;
                    }

                    if (ut.StartDate < startDate)
                    {
                        ut.EndDate = startDate.AddDays(-1);
                    }
                    else
                    {
                        ut.EndDate = ut.StartDate;
                    }
                    ut.UpdatedAt = DateTime.UtcNow;
                    closedCount++;
                }
            }

            // Check if user already has a membership with same team and start date
            var existingSameMembership = await _context.UserTeams
                .FirstOrDefaultAsync(ut => ut.UserInternalId == user.InternalId && ut.TeamId == targetTeam.Id && ut.StartDate == startDate);

            if (existingSameMembership != null)
            {
                existingSameMembership.EndDate = endDate;
                existingSameMembership.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newMembership = new UserTeam
                {
                    TeamId = targetTeam.Id,
                    UserInternalId = user.InternalId,
                    StartDate = startDate,
                    EndDate = endDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserTeams.Add(newMembership);
            }

            // Handle owner assignment
            bool isOwner = false;
            if (request.SetAsOwner || targetTeam.OwnerUserInternalId == user.InternalId)
            {
                targetTeam.OwnerUserInternalId = user.InternalId;
                targetTeam.UpdatedAt = DateTime.UtcNow;
                isOwner = true;
            }

            // If user was owner of any OTHER team and now has no active membership in that other team, clear owner of the other team
            var otherTeamsOwned = await _context.Teams
                .Where(t => t.Id != targetTeam.Id && t.OwnerUserInternalId == user.InternalId)
                .ToListAsync();

            foreach (var otherTeam in otherTeamsOwned)
            {
                var remainingActive = await _context.UserTeams
                    .AnyAsync(ut => ut.TeamId == otherTeam.Id && ut.UserInternalId == user.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
                if (!remainingActive)
                {
                    otherTeam.OwnerUserInternalId = null;
                    otherTeam.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<AdminMigrateTeamMemberResult>
            {
                Success = true,
                Data = new AdminMigrateTeamMemberResult
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    TargetTeamId = targetTeam.Id,
                    TargetTeamName = targetTeam.Name,
                    ClosedPeriodsCount = closedCount,
                    IsOwner = isOwner,
                    Message = $"Usuário '{user.Name}' migrado com sucesso para a equipe '{targetTeam.Name}'."
                },
                Message = $"Usuário '{user.Name}' migrado com sucesso para a equipe '{targetTeam.Name}'."
            });
        }

        [HttpGet("team-inconsistencies/export-xlsx")]
        public async Task<IActionResult> ExportTeamInconsistenciesXlsx()
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            var items = await ComputeTeamInconsistenciesAsync();

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Inconsistências de Equipes");

            var headers = new[]
            {
                "Nome",
                "Email",
                "Status",
                "Email Supervisor",
                "Nome Supervisor",
                "Equipe Supervisor",
                "Equipe Atual",
                "Equipe Proprietário",
                "1º Contrato",
                "Total Contratos",
                "Inconsistências Detectadas",
                "Histórico de Equipes",
                "Recomendação do Sistema"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }

            int row = 2;
            foreach (var item in items)
            {
                worksheet.Cells[row, 1].Value = item.UserName;
                worksheet.Cells[row, 2].Value = item.UserEmail;
                worksheet.Cells[row, 3].Value = item.IsActive ? "Ativo" : "Inativo";
                worksheet.Cells[row, 4].Value = item.ParentEmail ?? "—";
                worksheet.Cells[row, 5].Value = item.ParentUserName ?? "—";
                worksheet.Cells[row, 6].Value = item.ParentTeamName ?? "—";
                worksheet.Cells[row, 7].Value = item.CurrentTeamName ?? "Sem equipe";
                worksheet.Cells[row, 8].Value = item.OwnedTeamName ?? "—";
                worksheet.Cells[row, 9].Value = item.EarliestContractDate.HasValue
                    ? item.EarliestContractDate.Value.ToString("dd/MM/yyyy")
                    : "—";
                worksheet.Cells[row, 10].Value = item.TotalContractsCount;
                worksheet.Cells[row, 11].Value = string.Join(" | ", item.Inconsistencies.Select(i => i.Description));

                var historySummary = string.Join("; ", item.TeamHistory.Select(h =>
                    $"{h.TeamName} ({(h.TeamIsActive ? "Ativa" : "Inativa")}: {h.StartDate:dd/MM/yyyy} - {(h.EndDate.HasValue ? h.EndDate.Value.ToString("dd/MM/yyyy") : "Presente")})"));
                worksheet.Cells[row, 12].Value = string.IsNullOrWhiteSpace(historySummary) ? "Nenhum histórico" : historySummary;

                worksheet.Cells[row, 13].Value = item.Recommendation?.Reason ?? "—";

                row++;
            }

            worksheet.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"inconsistencias_equipes_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }

        [HttpGet("deleted-contracts")]
        public async Task<ActionResult<ApiResponse<PagedContractResponse>>> GetDeletedContracts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? contractNumber = null,
            [FromQuery] bool exactMatch = true,
            [FromQuery] int? teamId = null)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _context.Contracts
                .AsNoTracking()
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas).ThenInclude(um => um.Matricula)
                .Include(c => c.User!).ThenInclude(u => u.UserTeams).ThenInclude(ut => ut.Team)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User).ThenInclude(u => u.UserTeams).ThenInclude(ut => ut.Team)
                .Include(c => c.Group)
                .Include(c => c.ContractStatus)
                .Where(c => !c.IsActive);

            if (!string.IsNullOrWhiteSpace(contractNumber))
            {
                var norm = contractNumber.Trim();
                if (exactMatch)
                {
                    query = query.Where(c => c.ContractNumber == norm);
                }
                else
                {
                    query = query.Where(c => EF.Functions.Like(c.ContractNumber, $"%{norm}%"));
                }
            }

            if (teamId.HasValue)
            {
                var tId = teamId.Value;
                query = query.Where(c =>
                    (c.UserInternalId != null && _context.UserTeams.Any(ut =>
                        ut.TeamId == tId &&
                        ut.UserInternalId == c.UserInternalId.Value &&
                        c.SaleStartDate.Date >= ut.StartDate.Date &&
                        (ut.EndDate == null || c.SaleStartDate.Date <= ut.EndDate.Value.Date)))
                    ||
                    (c.UserInternalId == null && c.MatriculaId != null && _context.UserMatriculas.Any(um =>
                        um.MatriculaId == c.MatriculaId.Value && um.IsActive &&
                        _context.UserTeams.Any(ut =>
                            ut.TeamId == tId &&
                            ut.UserInternalId == um.UserInternalId &&
                            c.SaleStartDate.Date >= ut.StartDate.Date &&
                            (ut.EndDate == null || c.SaleStartDate.Date <= ut.EndDate.Value.Date))))
                );
            }

            var totalCount = await query.CountAsync();

            var contracts = await query
                .OrderByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = contracts.Select(c => MapToContractResponse(c)).ToList();

            return Ok(new ApiResponse<PagedContractResponse>
            {
                Success = true,
                Data = new PagedContractResponse
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            });
        }

        [HttpPost("deleted-contracts/{id}/restore")]
        public async Task<ActionResult<ApiResponse<object>>> RestoreDeletedContract(int id)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contract == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Contrato não encontrado."
                });
            }

            if (contract.IsActive)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "O contrato já se encontra ativo."
                });
            }

            var hasActiveDuplicate = await _context.Contracts
                .AnyAsync(c => c.ContractNumber == contract.ContractNumber && c.IsActive && c.Id != id);

            if (hasActiveDuplicate)
            {
                return Conflict(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Não é possível restaurar o contrato: já existe um contrato ativo com o número '{contract.ContractNumber}'."
                });
            }

            contract.IsActive = true;
            contract.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Contrato {contract.ContractNumber} restaurado com sucesso."
            });
        }

        private static ContractResponse MapToContractResponse(Contract contract)
        {
            var matriculaNumber = contract.Matricula?.MatriculaNumber
                ?? contract.TempMatricula
                ?? contract.User?.UserMatriculas?.FirstOrDefault(um => um.IsActive && um.IsOwner)?.Matricula?.MatriculaNumber
                ?? contract.User?.UserMatriculas?.FirstOrDefault(um => um.IsActive)?.Matricula?.MatriculaNumber;

            matriculaNumber = NormalizationUtils.NormalizeNumber(matriculaNumber);
            if (string.IsNullOrWhiteSpace(matriculaNumber))
            {
                matriculaNumber = null;
            }

            var statusName = contract.ContractStatus?.Name ?? "";
            var isAwaitingPayment = statusName.Equals("Active", StringComparison.OrdinalIgnoreCase) && contract.HasPayment == false;

            var contractUser = contract.User
                ?? contract.Matricula?.UserMatriculas?.FirstOrDefault(um => um.IsActive && um.IsOwner)?.User
                ?? contract.Matricula?.UserMatriculas?.FirstOrDefault(um => um.IsActive)?.User;

            var contractDate = contract.SaleStartDate.Date;
            var teamName = contractUser?.UserTeams?
                .Where(ut => contractDate >= ut.StartDate.Date && (ut.EndDate == null || contractDate <= ut.EndDate.Value.Date))
                .OrderByDescending(ut => ut.StartDate)
                .Select(ut => ut.Team?.Name)
                .FirstOrDefault(t => !string.IsNullOrEmpty(t));

            return new ContractResponse
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                UserId = contract.User?.Id,
                UserName = contract.User?.Name ?? "",
                TotalAmount = contract.TotalAmount,
                GroupId = contract.GroupId,
                GroupName = contract.Group?.Name ?? "",
                Status = statusName,
                ContractStartDate = contract.SaleStartDate,
                IsActive = contract.IsActive,
                CreatedAt = contract.CreatedAt,
                UpdatedAt = contract.UpdatedAt,
                ContractType = ContractTypeExtensions.ToApiString(contract.ContractType),
                Quota = contract.Quota,
                PvId = contract.PvId,
                CustomerName = contract.CustomerName,
                MatriculaId = contract.MatriculaId,
                MatriculaNumber = matriculaNumber,
                RawStatus = contract.RawStatus,
                HasPayment = contract.HasPayment,
                IsAwaitingPayment = isAwaitingPayment,
                IsRemappedToAwaitingPayment = false,
                TeamName = teamName
            };
        }
    }
}
