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

                // Explicitly displaying only 4 levels below the current user admin.
                allowedOwnerInternalIds = await GetDescendantsUpToLevel4Async(currentUserId);
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                if (caller != null)
                {
                    allowedOwnerInternalIds.Add(caller.InternalId);
                }
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
            var roleIdClaim = User.FindFirst("role_id")?.Value;
            if (roleIdClaim == "2") // Admin
            {
                return StatusCode(403, new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = "Administradores não podem criar equipes."
                });
            }

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
                Name = request.Name.Trim(),
                StoreId = request.StoreId
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

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                // Explicitly displaying only 4 levels below the current user admin.
                var allowedOwnerInternalIds = await GetDescendantsUpToLevel4Async(currentUserId);
                if (caller == null || (team.OwnerUserInternalId != caller.InternalId && (team.OwnerUserInternalId == null || !allowedOwnerInternalIds.Contains(team.OwnerUserInternalId.Value))))
                {
                    return Forbid();
                }

                if (request.OwnerUserId.HasValue)
                {
                    var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                    if (!allowedUserIds.Contains(request.OwnerUserId.Value))
                    {
                        return Forbid();
                    }
                }
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

            // Check Store editing permission: SuperAdmin or Admin owner of the team ONLY
            bool isAttemptingStoreEdit = request.StoreId.HasValue || request.ClearStore;
            if (isAttemptingStoreEdit)
            {
                bool isSuperAdmin = roleIdClaim == "1";
                bool isAdminOwner = false;

                if (Guid.TryParse(userIdClaim, out var callerUserId))
                {
                    var callerUser = await _userRepository.GetByIdAsync(callerUserId);
                    if (callerUser != null && team.OwnerUserInternalId.HasValue && team.OwnerUserInternalId.Value == callerUser.InternalId)
                    {
                        isAdminOwner = true;
                    }
                }

                if (!isSuperAdmin && !isAdminOwner)
                {
                    return StatusCode(403, new ApiResponse<TeamResponse>
                    {
                        Success = false,
                        Message = "Apenas o Superadmin ou o proprietário da equipe pode alterar a loja da equipe."
                    });
                }

                if (request.ClearStore)
                {
                    team.StoreId = null;
                }
                else if (request.StoreId.HasValue)
                {
                    team.StoreId = request.StoreId.Value;
                }
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
            var roleIdClaim = User.FindFirst("role_id")?.Value;
            if (roleIdClaim == "2") // Admin
            {
                return StatusCode(403, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Administradores não podem excluir equipes."
                });
            }

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

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdClaim, out var currentUserId);

            if (roleIdClaim == "2")
            {
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                // Explicitly displaying only 4 levels below the current user admin.
                var allowedOwnerInternalIds = await GetDescendantsUpToLevel4Async(currentUserId);
                if (caller == null || (team.OwnerUserInternalId != caller.InternalId && (team.OwnerUserInternalId == null || !allowedOwnerInternalIds.Contains(team.OwnerUserInternalId.Value))))
                {
                    return Forbid();
                }
            }

            var warnings = new List<string>();

            foreach (var memberReq in request.Members)
            {
                var user = await _userRepository.GetByIdAsync(memberReq.UserId);
                if (user == null) continue;

                if (roleIdClaim == "2")
                {
                    var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                    if (!allowedUserIds.Contains(user.Id))
                    {
                        var hasNoParent = user.ParentUserId == null;
                        var memberships = await _teamRepository.GetActiveMembershipsForUserAsync(user.InternalId, DateTime.UtcNow);
                        var hasNoTeam = !memberships.Any();

                        if (!hasNoParent && !hasNoTeam)
                        {
                            return BadRequest(new ApiResponse<TeamResponse>
                            {
                                Success = false,
                                Message = $"Cannot add user '{user.Name}' to team: Admin can only add descendants or users without a parent or without a team."
                            });
                        }
                    }
                }

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

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                // Explicitly displaying only 4 levels below the current user admin.
                var allowedOwnerInternalIds = await GetDescendantsUpToLevel4Async(currentUserId);
                if (caller == null || (team.OwnerUserInternalId != caller.InternalId && (team.OwnerUserInternalId == null || !allowedOwnerInternalIds.Contains(team.OwnerUserInternalId.Value))))
                {
                    return Forbid();
                }
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

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                // Explicitly displaying only 4 levels below the current user admin.
                var allowedOwnerInternalIds = await GetDescendantsUpToLevel4Async(currentUserId);
                if (caller == null || (team.OwnerUserInternalId != caller.InternalId && (team.OwnerUserInternalId == null || !allowedOwnerInternalIds.Contains(team.OwnerUserInternalId.Value))))
                {
                    return Forbid();
                }

                var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                if (!allowedUserIds.Contains(ownerUserId))
                {
                    return Forbid();
                }
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

        private async Task<HashSet<int>> GetDescendantsUpToLevel4Async(Guid userId)
        {
            // Explicitly displaying only 4 levels below the current user admin.
            // Level 0: The Admin themselves
            // Level 1: Immediate children
            // Level 2: Grandchildren
            // Level 3: Great-grandchildren
            // Level 4: Great-great-grandchildren
            
            var allLinks = await _userRepository.GetAllHierarchyLinksAsync();
            var childrenMap = new Dictionary<Guid, List<Guid>>();

            foreach (var link in allLinks)
            {
                if (link.ParentUserId.HasValue)
                {
                    if (!childrenMap.ContainsKey(link.ParentUserId.Value))
                        childrenMap[link.ParentUserId.Value] = new List<Guid>();
                    childrenMap[link.ParentUserId.Value].Add(link.Id);
                }
            }

            var descendants = new HashSet<Guid>();
            var queue = new Queue<(Guid Id, int Depth)>();
            queue.Enqueue((userId, 0));

            while (queue.Count > 0)
            {
                var (currentId, currentDepth) = queue.Dequeue();
                
                if (currentDepth > 0)
                {
                    descendants.Add(currentId);
                }

                if (currentDepth < 4)
                {
                    if (childrenMap.TryGetValue(currentId, out var children))
                    {
                        foreach (var childId in children)
                        {
                            queue.Enqueue((childId, currentDepth + 1));
                        }
                    }
                }
            }

            var internalIdMap = allLinks.ToDictionary(l => l.Id, l => l.InternalId);
            return descendants
                .Where(id => internalIdMap.ContainsKey(id))
                .Select(id => internalIdMap[id])
                .ToHashSet();
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
            var members = t.UserTeams
                .Where(ut => ut.User != null && ut.User.IsActive)
                .Select(ut => MapToMemberResponse(ut, t.OwnerUserInternalId))
                .ToList();
            var owner = members.FirstOrDefault(m => m.UserInternalId == t.OwnerUserInternalId);

            return new TeamResponse
            {
                Id = t.Id,
                Name = t.Name,
                StoreId = t.StoreId,
                StoreName = t.Store?.Name,
                StoreState = t.Store?.State,
                Owner = owner,
                Members = members,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }
    }
}
