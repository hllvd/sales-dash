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
            if (request == null)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Requisição inválida."
                });
            }

            bool hasParentEmail = !string.IsNullOrWhiteSpace(request.ParentEmail);
            bool hasMatricula = !string.IsNullOrWhiteSpace(request.Matricula);

            if (!hasParentEmail && !hasMatricula)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Informe o e-mail do superior ou a matrícula."
                });
            }

            if (hasParentEmail && hasMatricula)
            {
                return BadRequest(new ApiResponse<BatchAssignTeamResult>
                {
                    Success = false,
                    Message = "Informe apenas o e-mail do superior ou a matrícula, não ambos."
                });
            }

            if (hasParentEmail && !request.ParentEmail!.Contains("@"))
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

            // 3. Resolve team
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

            // 4. Resolve target users
            List<User> targetUsers = new List<User>();

            if (hasParentEmail)
            {
                var parentUser = await _userRepository.GetByEmailAsync(request.ParentEmail!.Trim());
                if (parentUser == null)
                {
                    return BadRequest(new ApiResponse<BatchAssignTeamResult>
                    {
                        Success = false,
                        Message = "Superior com o e-mail especificado não foi encontrado ou está inativo."
                    });
                }

                targetUsers = await _context.Users
                    .Include(u => u.ParentUser)
                    .Where(u => u.IsActive && u.ParentUserId == parentUser.Id)
                    .ToListAsync();
            }
            else // hasMatricula
            {
                var matriculaNumber = request.Matricula!.Trim().ToLower();
                var user = await _context.Users
                    .Include(u => u.ParentUser)
                    .Where(u => u.IsActive && _context.UserMatriculas.Any(um => um.UserInternalId == u.InternalId && um.IsActive && um.Matricula.MatriculaNumber.ToLower() == matriculaNumber))
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return BadRequest(new ApiResponse<BatchAssignTeamResult>
                    {
                        Success = false,
                        Message = "Usuário com a matrícula especificada não foi encontrado ou está inativo."
                    });
                }

                targetUsers = new List<User> { user };
            }

            var result = new BatchAssignTeamResult();
            var startDate = request.StartDate ?? DateTime.UtcNow;

            foreach (var targetUser in targetUsers)
            {
                var activeMembership = team.UserTeams
                    .FirstOrDefault(ut => ut.UserInternalId == targetUser.InternalId && ut.EndDate == null);

                if (activeMembership != null)
                {
                    if (!request.OverrideExisting)
                    {
                        result.Skipped.Add(new SkippedUserSummary
                        {
                            Id = targetUser.Id,
                            Name = targetUser.Name,
                            Email = targetUser.Email,
                            CurrentParentEmail = targetUser.ParentUser?.Email,
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
                            Id = targetUser.Id,
                            Name = targetUser.Name,
                            Email = targetUser.Email
                        });
                    }
                }
                else
                {
                    var userTeam = new UserTeam
                    {
                        TeamId = team.Id,
                        UserInternalId = targetUser.InternalId,
                        StartDate = startDate,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserTeams.Add(userTeam);

                    result.Added.Add(new AddedMemberSummary
                    {
                        Id = targetUser.Id,
                        Name = targetUser.Name,
                        Email = targetUser.Email
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

        [HttpPost("users/merge")]
        public async Task<ActionResult<ApiResponse<MergeUsersResult>>> BatchMergeUsers([FromBody] MergeUsersRequest request)
        {
            // 1. Authorize: strictly superadmin@salesapp.com (or superadmin@test.com for integration tests)
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            if (emailClaim != "superadmin@salesapp.com" && emailClaim != "superadmin@test.com")
            {
                return Forbid();
            }

            if (request == null || request.Pairs == null || request.Pairs.Count == 0)
            {
                return BadRequest(new ApiResponse<MergeUsersResult>
                {
                    Success = false,
                    Message = "Pelo menos um par de e-mails deve ser fornecido."
                });
            }

            var result = new MergeUsersResult
            {
                IsDryRun = request.DryRun
            };

            foreach (var pair in request.Pairs)
            {
                var mainEmailClean = pair.MainEmail?.Trim();
                var duplicateEmailClean = pair.DuplicateEmail?.Trim();

                var pairResult = new MergeUserPairResult
                {
                    MainEmail = mainEmailClean ?? string.Empty,
                    DuplicateEmail = duplicateEmailClean ?? string.Empty,
                    DuplicateDeactivated = request.DeactivateDuplicate
                };

                if (string.IsNullOrWhiteSpace(mainEmailClean) || string.IsNullOrWhiteSpace(duplicateEmailClean))
                {
                    pairResult.Error = "E-mail principal e e-mail duplicado são obrigatórios.";
                    result.Pairs.Add(pairResult);
                    continue;
                }

                if (mainEmailClean.Equals(duplicateEmailClean, StringComparison.OrdinalIgnoreCase))
                {
                    pairResult.Error = "O e-mail principal e o e-mail duplicado não podem ser iguais.";
                    result.Pairs.Add(pairResult);
                    continue;
                }

                var mainUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == mainEmailClean.ToLower());
                var duplicateUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == duplicateEmailClean.ToLower());

                if (mainUser == null)
                {
                    pairResult.Error = $"Usuário principal '{mainEmailClean}' não foi encontrado.";
                    result.Pairs.Add(pairResult);
                    continue;
                }

                if (duplicateUser == null)
                {
                    pairResult.Error = $"Usuário duplicado '{duplicateEmailClean}' não foi encontrado.";
                    result.Pairs.Add(pairResult);
                    continue;
                }

                // Fetch contracts linked to duplicateUser
                var duplicateContracts = await _context.Contracts
                    .Where(c => c.UserInternalId == duplicateUser.InternalId)
                    .ToListAsync();

                // Fetch UserMatriculas linked to duplicateUser and mainUser
                var duplicateMatriculas = await _context.UserMatriculas
                    .Where(um => um.UserInternalId == duplicateUser.InternalId)
                    .ToListAsync();
                var mainMatriculas = await _context.UserMatriculas
                    .Where(um => um.UserInternalId == mainUser.InternalId)
                    .ToListAsync();

                // Fetch Child users where ParentUserId == duplicateUser.Id
                var childUsers = await _context.Users
                    .Where(u => u.ParentUserId == duplicateUser.Id)
                    .ToListAsync();

                // Fetch UserTeams linked to duplicateUser and mainUser
                var duplicateTeams = await _context.UserTeams
                    .Where(ut => ut.UserInternalId == duplicateUser.InternalId)
                    .ToListAsync();
                var mainTeams = await _context.UserTeams
                    .Where(ut => ut.UserInternalId == mainUser.InternalId)
                    .ToListAsync();

                pairResult.ContractsMigrated = duplicateContracts.Count;
                pairResult.MatriculasMigrated = duplicateMatriculas.Count;
                pairResult.ChildUsersMigrated = childUsers.Count;
                pairResult.TeamMembershipsMigrated = duplicateTeams.Count;

                if (!request.DryRun)
                {
                    // 1. Migrate Contracts
                    foreach (var contract in duplicateContracts)
                    {
                        contract.UserInternalId = mainUser.InternalId;
                        contract.UpdatedAt = DateTime.UtcNow;
                    }

                    // 2. Migrate UserMatriculas
                    foreach (var um in duplicateMatriculas)
                    {
                        var existingMainUM = mainMatriculas.FirstOrDefault(m => m.MatriculaId == um.MatriculaId);
                        if (existingMainUM != null)
                        {
                            if (um.IsOwner)
                            {
                                existingMainUM.IsOwner = true;
                                existingMainUM.UpdatedAt = DateTime.UtcNow;
                            }
                            _context.UserMatriculas.Remove(um);
                        }
                        else
                        {
                            um.UserInternalId = mainUser.InternalId;
                            um.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // 3. Migrate Child Users
                    foreach (var child in childUsers)
                    {
                        if (!await _userRepository.WouldCreateCycleAsync(child.Id, mainUser.Id))
                        {
                            child.ParentUserId = mainUser.Id;
                            child.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // 4. Migrate UserTeams
                    foreach (var ut in duplicateTeams)
                    {
                        var existingMainTeam = mainTeams.FirstOrDefault(t => t.TeamId == ut.TeamId && t.EndDate == null);
                        if (existingMainTeam != null)
                        {
                            _context.UserTeams.Remove(ut);
                        }
                        else
                        {
                            ut.UserInternalId = mainUser.InternalId;
                            ut.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // 5. Optionally deactivate duplicate user
                    if (request.DeactivateDuplicate)
                    {
                        duplicateUser.IsActive = false;
                        duplicateUser.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                }

                result.Pairs.Add(pairResult);
            }

            return Ok(new ApiResponse<MergeUsersResult>
            {
                Success = true,
                Data = result,
                Message = request.DryRun
                    ? $"Pré-visualização concluída para {result.Pairs.Count} par(es)."
                    : $"Consolidação de usuários concluída com sucesso para {result.Pairs.Count} par(es)."
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

