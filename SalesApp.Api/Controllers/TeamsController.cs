using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly IUserHierarchyService _userHierarchyService;

        public TeamsController(
            ITeamRepository teamRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            IUserHierarchyService userHierarchyService)
        {
            _teamRepository = teamRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _userHierarchyService = userHierarchyService;
        }

        [HttpGet]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<List<TeamResponse>>>> GetTeams()
        {
            var roleIdClaim = User.FindFirst("role_id")?.Value;
            HashSet<int>? allowedOwnerInternalIds = null;

            if (roleIdClaim != "1") // Not a Superadmin
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized();
                }

                allowedOwnerInternalIds = await _userHierarchyService.GetDescendantInternalIdsAsync(currentUserId);
            }

            var teams = await _teamRepository.GetAllAsync(allowedOwnerInternalIds);
            var responses = teams.Select(MapToTeamResponse).ToList();

            return Ok(new ApiResponse<List<TeamResponse>>
            {
                Success = true,
                Data = responses,
                Message = _messageService.Get(AppMessage.TeamsRetrievedSuccessfully)
            });
        }

        [HttpGet("{id}")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> GetTeam(int id)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = MapToTeamResponse(team),
                Message = _messageService.Get(AppMessage.TeamRetrievedSuccessfully)
            });
        }

        [HttpPost]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> CreateTeam(CreateTeamRequest request)
        {
            if (await _teamRepository.NameExistsAsync(request.Name))
            {
                return BadRequest(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNameAlreadyExists)
                });
            }

            var team = new Team
            {
                Name = request.Name.Trim()
            };

            var createdTeam = await _teamRepository.CreateAsync(team);
            var warnings = new List<string>();

            if (request.Members != null && request.Members.Any())
            {
                foreach (var memberReq in request.Members)
                {
                    var user = await _userRepository.GetByIdAsync(memberReq.UserId);
                    if (user == null) continue;

                    var start = memberReq.StartDate ?? DateTime.UtcNow.AddYears(-8);

                    // Resolve overlaps on other teams
                    var overlaps = await _teamRepository.FindOverlappingMembershipsAsync(user.InternalId, start, null);
                    foreach (var overlap in overlaps)
                    {
                        if (overlap.TeamId != createdTeam.Id)
                        {
                            overlap.EndDate = DateTime.UtcNow;
                            overlap.UpdatedAt = DateTime.UtcNow;
                            await _teamRepository.UpdateAsync(overlap.Team);

                            warnings.Add(_messageService.Get(AppMessage.UserRemovedFromTeamConflict, user.Name, overlap.Team.Name));
                        }
                    }

                    var userTeam = new UserTeam
                    {
                        TeamId = createdTeam.Id,
                        UserInternalId = user.InternalId,
                        StartDate = start
                    };

                    await _teamRepository.AddMemberAsync(userTeam);
                }
            }

            // Reload team to get complete members lists
            var reloadedTeam = await _teamRepository.GetByIdAsync(createdTeam.Id);
            var response = MapToTeamResponse(reloadedTeam ?? createdTeam);
            response.Warnings = warnings;

            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = response,
                Message = _messageService.Get(AppMessage.TeamCreatedSuccessfully)
            });
        }

        [HttpPut("{id}")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> UpdateTeam(int id, UpdateTeamRequest request)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().ToLower() != team.Name.ToLower())
            {
                if (await _teamRepository.NameExistsAsync(request.Name, id))
                {
                    return BadRequest(new ApiResponse<TeamResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.TeamNameAlreadyExists)
                    });
                }
                team.Name = request.Name.Trim();
            }

            if (request.OwnerUserId.HasValue)
            {
                var ownerUser = await _userRepository.GetByIdAsync(request.OwnerUserId.Value);
                if (ownerUser == null)
                {
                    return BadRequest(new ApiResponse<TeamResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.UserNotFound)
                    });
                }

                // Verify Owner is also a Member (must be in UserTeams)
                var isMember = team.UserTeams.Any(ut => ut.UserInternalId == ownerUser.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
                if (!isMember)
                {
                    return BadRequest(new ApiResponse<TeamResponse>
                    {
                        Success = false,
                        Message = "O proprietário deve ser um membro ativo da equipe."
                    });
                }

                team.OwnerUserInternalId = ownerUser.InternalId;
            }

            await _teamRepository.UpdateAsync(team);

            var reloadedTeam = await _teamRepository.GetByIdAsync(id);
            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = MapToTeamResponse(reloadedTeam ?? team),
                Message = _messageService.Get(AppMessage.TeamUpdatedSuccessfully)
            });
        }

        [HttpDelete("{id}")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTeam(int id)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            await _teamRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.TeamDeletedSuccessfully)
            });
        }

        [HttpPost("{id}/members")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> AddMembers(int id, AddMembersRequest request)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            var warnings = new List<string>();

            foreach (var memberReq in request.Members)
            {
                var user = await _userRepository.GetByIdAsync(memberReq.UserId);
                if (user == null) continue;

                var start = memberReq.StartDate ?? DateTime.UtcNow.AddYears(-8);

                // Auto-close overlapping memberships on other teams
                var overlaps = await _teamRepository.FindOverlappingMembershipsAsync(user.InternalId, start, null);
                foreach (var overlap in overlaps)
                {
                    if (overlap.TeamId != id)
                    {
                        overlap.EndDate = DateTime.UtcNow;
                        overlap.UpdatedAt = DateTime.UtcNow;
                        await _teamRepository.UpdateAsync(overlap.Team);

                        warnings.Add(_messageService.Get(AppMessage.UserRemovedFromTeamConflict, user.Name, overlap.Team.Name));
                    }
                }

                // Check if already an active member of this team
                var alreadyMember = team.UserTeams.Any(ut => ut.UserInternalId == user.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
                if (!alreadyMember)
                {
                    var userTeam = new UserTeam
                    {
                        TeamId = id,
                        UserInternalId = user.InternalId,
                        StartDate = start
                    };
                    await _teamRepository.AddMemberAsync(userTeam);
                }
            }

            var reloadedTeam = await _teamRepository.GetByIdAsync(id);
            var response = MapToTeamResponse(reloadedTeam ?? team);
            response.Warnings = warnings;

            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = response,
                Message = "Membros adicionados com sucesso"
            });
        }

        [HttpDelete("{id}/members/{userId}")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> RemoveMember(int id, Guid userId)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            await _teamRepository.RemoveMemberAsync(id, user.InternalId);

            // If the removed member was the owner, unset owner
            if (team.OwnerUserInternalId == user.InternalId)
            {
                team.OwnerUserInternalId = null;
                await _teamRepository.UpdateAsync(team);
            }

            var reloadedTeam = await _teamRepository.GetByIdAsync(id);
            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = MapToTeamResponse(reloadedTeam ?? team),
                Message = "Membro removido com sucesso"
            });
        }

        [HttpPost("{id}/owner")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> SetOwner(int id, [FromBody] Guid ownerUserId)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            var user = await _userRepository.GetByIdAsync(ownerUserId);
            if (user == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            // Verify owner is also a member
            var isMember = team.UserTeams.Any(ut => ut.UserInternalId == user.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
            if (!isMember)
            {
                return BadRequest(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = "O proprietário deve ser um membro ativo da equipe."
                });
            }

            await _teamRepository.SetOwnerAsync(id, user.InternalId);

            var reloadedTeam = await _teamRepository.GetByIdAsync(id);
            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = MapToTeamResponse(reloadedTeam ?? team),
                Message = "Proprietário definido com sucesso"
            });
        }

        [HttpPut("{id}/members/{userId}")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> UpdateMemberDates(int id, Guid userId, [FromBody] UpdateMemberDatesRequest request)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.TeamNotFound)
                });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            var membership = team.UserTeams.FirstOrDefault(ut => ut.UserInternalId == user.InternalId && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
            if (membership == null)
            {
                membership = team.UserTeams.FirstOrDefault(ut => ut.UserInternalId == user.InternalId);
            }

            if (membership == null)
            {
                return BadRequest(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = "Usuário não pertence a esta equipe."
                });
            }

            if (request.EndDate.HasValue && request.StartDate > request.EndDate.Value)
            {
                return BadRequest(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = "A data de início deve ser anterior à data de fim."
                });
            }

            membership.StartDate = request.StartDate;
            membership.EndDate = request.EndDate;
            membership.UpdatedAt = DateTime.UtcNow;

            await _teamRepository.UpdateAsync(team);

            var reloadedTeam = await _teamRepository.GetByIdAsync(id);
            return Ok(new ApiResponse<TeamResponse>
            {
                Success = true,
                Data = MapToTeamResponse(reloadedTeam ?? team),
                Message = "Datas atualizadas com sucesso"
            });
        }

        private TeamMemberResponse MapToMemberResponse(UserTeam ut, int? ownerId)
        {
            return new TeamMemberResponse
            {
                UserId = ut.User.Id,
                UserInternalId = ut.UserInternalId,
                UserName = ut.User.Name,
                UserEmail = ut.User.Email,
                StartDate = ut.StartDate,
                EndDate = ut.EndDate,
                IsActive = ut.EndDate == null || ut.EndDate > DateTime.UtcNow,
                IsOwner = ut.UserInternalId == ownerId
            };
        }

        private TeamResponse MapToTeamResponse(Team t)
        {
            var members = t.UserTeams.Select(ut => MapToMemberResponse(ut, t.OwnerUserInternalId)).ToList();
            var owner = members.FirstOrDefault(m => m.UserInternalId == t.OwnerUserInternalId);

            return new TeamResponse
            {
                Id = t.Id,
                Name = t.Name,
                Owner = owner,
                Members = members,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }
    }
}
