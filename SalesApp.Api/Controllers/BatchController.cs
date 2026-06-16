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
using SalesApp.Repositories;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BatchController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserHierarchyService _hierarchyService;
        private readonly AppDbContext _context;

        public BatchController(
            IUserRepository userRepository,
            IUserHierarchyService hierarchyService,
            AppDbContext context)
        {
            _userRepository = userRepository;
            _hierarchyService = hierarchyService;
            _context = context;
        }

        [HttpPost("users/parent")]
        public async Task<ActionResult<ApiResponse<BatchUpdateParentResult>>> BatchUpdateParent([FromBody] BatchUpdateParentRequest request)
        {
            // 1. Authorize: strictly superadmin@salesapp.com (or superadmin@test.com for integration tests)
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            if (emailClaim != "superadmin@salesapp.com" && emailClaim != "superadmin@test.com")
            {
                return Forbid();
            }

            // 2. Validate input parameters
            if (request == null || string.IsNullOrWhiteSpace(request.ParentEmail))
            {
                return BadRequest(new ApiResponse<BatchUpdateParentResult>
                {
                    Success = false,
                    Message = "E-mail do superior é obrigatório."
                });
            }

            if (!request.ParentEmail.Contains("@"))
            {
                return BadRequest(new ApiResponse<BatchUpdateParentResult>
                {
                    Success = false,
                    Message = "E-mail do superior inválido."
                });
            }

            if (!request.TeamId.HasValue && string.IsNullOrWhiteSpace(request.Matricula))
            {
                return BadRequest(new ApiResponse<BatchUpdateParentResult>
                {
                    Success = false,
                    Message = "Pelo menos um filtro (Equipe ou Matrícula) deve ser fornecido."
                });
            }

            // 3. Resolve parent user
            var parentUser = await _userRepository.GetByEmailAsync(request.ParentEmail.Trim());
            if (parentUser == null)
            {
                return BadRequest(new ApiResponse<BatchUpdateParentResult>
                {
                    Success = false,
                    Message = "Superior com o e-mail especificado não foi encontrado ou está inativo."
                });
            }

            // 4. Query target users
            var query = _context.Users
                .Include(u => u.ParentUser)
                .Where(u => u.IsActive);

            if (request.TeamId.HasValue)
            {
                // Join with UserTeam (active)
                query = query.Where(u => _context.UserTeams.Any(ut => ut.UserInternalId == u.InternalId && ut.TeamId == request.TeamId.Value && ut.EndDate == null));
            }

            if (!string.IsNullOrWhiteSpace(request.Matricula))
            {
                var matriculaNumber = request.Matricula.Trim().ToLower();
                // Join with UserMatricula & Matricula (active)
                query = query.Where(u => _context.UserMatriculas.Any(um => um.UserInternalId == u.InternalId && um.IsActive && um.Matricula.MatriculaNumber.ToLower() == matriculaNumber));
            }

            var usersToProcess = await query.ToListAsync();

            var result = new BatchUpdateParentResult();

            foreach (var user in usersToProcess)
            {
                // Validation checks
                if (user.Id == parentUser.Id)
                {
                    result.Skipped.Add(new SkippedUserSummary
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        CurrentParentEmail = user.ParentUser?.Email,
                        Reason = "O usuário não pode ser superior de si mesmo"
                    });
                    continue;
                }

                if (user.ParentUserId.HasValue && !request.OverrideExisting)
                {
                    result.Skipped.Add(new SkippedUserSummary
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        CurrentParentEmail = user.ParentUser?.Email,
                        Reason = "Usuário já possui superior e sobrescrever está desativado"
                    });
                    continue;
                }

                // Check circular reference
                if (await _userRepository.WouldCreateCycleAsync(user.Id, parentUser.Id))
                {
                    result.Skipped.Add(new SkippedUserSummary
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        CurrentParentEmail = user.ParentUser?.Email,
                        Reason = "Esta alteração criaria uma referência circular na hierarquia"
                    });
                    continue;
                }

                // If no changes needed
                if (user.ParentUserId == parentUser.Id)
                {
                    result.Skipped.Add(new SkippedUserSummary
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        CurrentParentEmail = user.ParentUser?.Email,
                        Reason = "O usuário já possui este superior atribuído"
                    });
                    continue;
                }

                // Valid update path
                var oldParentEmail = user.ParentUser?.Email;
                user.ParentUserId = parentUser.Id;
                
                // Recalculate levels recursively
                await UpdateUserHierarchyLevelsAsync(user, parentUser.Level + 1);

                await _userRepository.UpdateAsync(user);

                result.Modified.Add(new ModifiedUserSummary
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    OldParentEmail = oldParentEmail,
                    NewParentEmail = parentUser.Email
                });
            }

            return Ok(new ApiResponse<BatchUpdateParentResult>
            {
                Success = true,
                Data = result,
                Message = $"Processamento concluído. {result.Modified.Count} atualizados, {result.Skipped.Count} ignorados."
            });
        }

        [HttpPost("team/assign")]
        public async Task<ActionResult<ApiResponse<BatchAssignTeamResult>>> BatchAssignTeam([FromBody] BatchAssignTeamRequest request)
        {
            // 1. Authorize: strictly superadmin@salesapp.com (or superadmin@test.com for integration tests)
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            if (emailClaim != "superadmin@salesapp.com" && emailClaim != "superadmin@test.com")
            {
                return Forbid();
            }

            // 2. Validate input parameters
            if (request == null || string.IsNullOrWhiteSpace(request.ParentEmail))
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "E-mail do superior é obrigatório."
                });
            }

            if (!request.ParentEmail.Contains("@"))
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "E-mail do superior inválido."
                });
            }

            if (request.TeamId <= 0)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Identificador da equipe de destino é obrigatório."
                });
            }

            // 3. Resolve parent user
            var parentUser = await _userRepository.GetByEmailAsync(request.ParentEmail.Trim());
            if (parentUser == null)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Superior com o e-mail especificado não foi encontrado ou está inativo."
                });
            }

            // 4. Resolve team
            var team = await _context.Teams
                .Include(t => t.UserTeams)
                .FirstOrDefaultAsync(t => t.Id == request.TeamId);
            if (team == null)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Equipe não encontrada."
                });
            }

            // 5. Query direct children
            var children = await _context.Users
                .Where(u => u.IsActive && u.ParentUserId == parentUser.Id)
                .ToListAsync();

            var result = new BatchAssignTeamResult();
            var startDate = request.StartDate ?? DateTime.UtcNow;

            foreach (var child in children)
            {
                var activeMembership = team.UserTeams
                    .FirstOrDefault(ut => ut.UserInternalId == child.InternalId && ut.EndDate == null);

                if (activeMembership != null)
                {
                    if (!request.OverrideExisting)
                    {
                        result.Skipped.Add(new SkippedUserSummary
                        {
                            Id = child.Id,
                            Name = child.Name,
                            Email = child.Email,
                            CurrentParentEmail = parentUser.Email,
                            Reason = "Usuário já é membro ativo desta equipe"
                        });
                        continue;
                    }
                    else
                    {
                        activeMembership.StartDate = startDate;
                        activeMembership.UpdatedAt = DateTime.UtcNow;

                        result.Added.Add(new AddedMemberSummary
                        {
                            Id = child.Id,
                            Name = child.Name,
                            Email = child.Email
                        });
                    }
                }
                else
                {
                    var userTeam = new UserTeam
                    {
                        TeamId = team.Id,
                        UserInternalId = child.InternalId,
                        StartDate = startDate,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserTeams.Add(userTeam);

                    result.Added.Add(new AddedMemberSummary
                    {
                        Id = child.Id,
                        Name = child.Name,
                        Email = child.Email
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<BatchAssignTeamResult>
            {
                Success = true,
                Data = result,
                Message = $"Processamento concluído. {result.Added.Count} adicionados, {result.Skipped.Count} ignorados."
            });
        }

        private async Task UpdateUserHierarchyLevelsAsync(User user, int newLevel)
        {
            user.Level = newLevel;
            var children = await _context.Users
                .Where(u => u.ParentUserId == user.Id && u.IsActive)
                .ToListAsync();

            foreach (var child in children)
            {
                await UpdateUserHierarchyLevelsAsync(child, newLevel + 1);
                await _userRepository.UpdateAsync(child);
            }
        }
    }
}
