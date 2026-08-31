using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
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
        private readonly AppDbContext _context;

        public TeamsController(
            ITeamRepository teamRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            IUserHierarchyService userHierarchyService,
            AppDbContext context)
        {
            _teamRepository = teamRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _userHierarchyService = userHierarchyService;
            _context = context;
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

            if (request.EndDate.HasValue && (request.EndDate.Value - request.StartDate).TotalDays < 7)
            {
                return BadRequest(new ApiResponse<TeamResponse>
                {
                    Success = false,
                    Message = "O período na equipe deve ter duração mínima de 1 semana (7 dias)."
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

        [HttpGet("calendar")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<List<TeamCalendarUserResponse>>>> GetTeamCalendar()
        {
            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized();
            }

            var allLinks = await _userRepository.GetAllHierarchyLinksAsync();
            var childrenMap = new Dictionary<Guid, List<Guid>>();
            var allIds = allLinks.Select(l => l.Id).ToHashSet();

            foreach (var link in allLinks)
            {
                if (link.ParentUserId.HasValue)
                {
                    if (!childrenMap.ContainsKey(link.ParentUserId.Value))
                        childrenMap[link.ParentUserId.Value] = new List<Guid>();
                    childrenMap[link.ParentUserId.Value].Add(link.Id);
                }
            }

            var userLevels = new Dictionary<Guid, int>();
            var queue = new Queue<(Guid Id, int Depth)>();

            if (roleIdClaim == "1") // Superadmin
            {
                // Traverse from roots or top-level nodes to identify levels 1, 2, 3
                var rootIds = allLinks.Where(l => !l.ParentUserId.HasValue || !allIds.Contains(l.ParentUserId.Value)).Select(l => l.Id).ToList();
                foreach (var rootId in rootIds)
                {
                    queue.Enqueue((rootId, 0));
                }
            }
            else // Admin
            {
                queue.Enqueue((currentUserId, 0));
            }

            while (queue.Count > 0)
            {
                var (curId, curDepth) = queue.Dequeue();
                if (curDepth >= 1 && curDepth <= 3)
                {
                    if (!userLevels.ContainsKey(curId))
                    {
                        userLevels[curId] = curDepth;
                    }
                }

                if (curDepth < 3 && childrenMap.TryGetValue(curId, out var kids))
                {
                    foreach (var kid in kids)
                    {
                        queue.Enqueue((kid, curDepth + 1));
                    }
                }
            }

            var targetUserGuids = userLevels.Keys.ToList();
            if (!targetUserGuids.Any())
            {
                return Ok(new ApiResponse<List<TeamCalendarUserResponse>>
                {
                    Success = true,
                    Data = new List<TeamCalendarUserResponse>(),
                    Message = "Nenhum usuário encontrado na hierarquia."
                });
            }

            var users = await _context.Users
                .AsNoTracking()
                .Include(u => u.ParentUser)
                .Where(u => targetUserGuids.Contains(u.Id) && u.IsActive)
                .ToListAsync();

            var userInternalIds = users.Select(u => u.InternalId).ToList();
            var allMemberships = await _teamRepository.GetAllMembershipsForUsersAsync(userInternalIds);

            // Query earliest contract date for each user
            var earliestContractDates = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.UserInternalId.HasValue && userInternalIds.Contains(c.UserInternalId.Value) && c.IsActive)
                .GroupBy(c => c.UserInternalId!.Value)
                .Select(g => new { UserInternalId = g.Key, EarliestDate = g.Min(c => c.SaleStartDate) })
                .ToDictionaryAsync(x => x.UserInternalId, x => (DateTime?)x.EarliestDate);

            var membershipsByUser = allMemberships
                .GroupBy(m => m.UserInternalId)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.StartDate).ToList());

            var result = new List<TeamCalendarUserResponse>();
            foreach (var user in users)
            {
                var history = membershipsByUser.TryGetValue(user.InternalId, out var mems)
                    ? mems.Select(m => new TeamCalendarUserHistoryItem
                    {
                        UserTeamId = m.Id,
                        TeamId = m.TeamId,
                        TeamName = m.Team?.Name ?? $"Equipe #{m.TeamId}",
                        StartDate = m.StartDate,
                        EndDate = m.EndDate,
                        IsActive = m.EndDate == null || m.EndDate > DateTime.UtcNow
                    }).ToList()
                    : new List<TeamCalendarUserHistoryItem>();

                var activeTeam = history.FirstOrDefault(h => h.IsActive);

                result.Add(new TeamCalendarUserResponse
                {
                    UserId = user.Id,
                    UserInternalId = user.InternalId,
                    UserName = user.Name,
                    UserEmail = user.Email,
                    CurrentTeamName = activeTeam?.TeamName,
                    CurrentTeamId = activeTeam?.TeamId,
                    HierarchyLevel = userLevels.TryGetValue(user.Id, out var lvl) ? lvl : 1,
                    ParentUserName = user.ParentUser?.Name,
                    EarliestContractDate = earliestContractDates.TryGetValue(user.InternalId, out var dt) ? dt : null,
                    TeamHistory = history
                });
            }

            return Ok(new ApiResponse<List<TeamCalendarUserResponse>>
            {
                Success = true,
                Data = result.OrderBy(u => u.HierarchyLevel).ThenBy(u => u.UserName).ToList(),
                Message = "Calendário de equipes recuperado com sucesso"
            });
        }

        [HttpGet("calendar/contract-preview")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<CalendarContractPreviewResponse>>> GetContractPreview(
            [FromQuery] Guid userId,
            [FromQuery] DateTime boundaryDate)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<CalendarContractPreviewResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                if (!allowedUserIds.Contains(userId))
                {
                    return Forbid();
                }
            }

            var olderContracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Matricula)
                .Where(c => c.UserInternalId == user.InternalId && c.IsActive && c.SaleStartDate < boundaryDate)
                .OrderByDescending(c => c.SaleStartDate)
                .Take(5)
                .Select(c => new CalendarContractPreviewItem
                {
                    ContractId = c.Id,
                    ContractNumber = c.ContractNumber,
                    SaleStartDate = c.SaleStartDate,
                    CustomerName = c.CustomerName,
                    MatriculaNumber = c.Matricula != null ? c.Matricula.MatriculaNumber : c.TempMatricula,
                    TotalAmount = c.TotalAmount
                })
                .ToListAsync();

            var newerContracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Matricula)
                .Where(c => c.UserInternalId == user.InternalId && c.IsActive && c.SaleStartDate >= boundaryDate)
                .OrderBy(c => c.SaleStartDate)
                .Take(5)
                .Select(c => new CalendarContractPreviewItem
                {
                    ContractId = c.Id,
                    ContractNumber = c.ContractNumber,
                    SaleStartDate = c.SaleStartDate,
                    CustomerName = c.CustomerName,
                    MatriculaNumber = c.Matricula != null ? c.Matricula.MatriculaNumber : c.TempMatricula,
                    TotalAmount = c.TotalAmount
                })
                .ToListAsync();

            return Ok(new ApiResponse<CalendarContractPreviewResponse>
            {
                Success = true,
                Data = new CalendarContractPreviewResponse
                {
                    OlderTeamContracts = olderContracts,
                    NewerTeamContracts = newerContracts
                },
                Message = "Preview de contratos recuperado com sucesso"
            });
        }

        [HttpPut("calendar/adjust-boundary")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamCalendarUserResponse>>> AdjustTeamBoundary([FromBody] AdjustTeamBoundaryRequest request)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new ApiResponse<TeamCalendarUserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                if (!allowedUserIds.Contains(request.UserId))
                {
                    return Forbid();
                }
            }

            var userTeams = await _context.UserTeams
                .Include(ut => ut.Team)
                .Where(ut => ut.UserInternalId == user.InternalId)
                .ToListAsync();

            UserTeam? olderUserTeam = null;
            UserTeam? newerUserTeam = null;

            if (request.OlderTeamId.HasValue)
            {
                olderUserTeam = userTeams.FirstOrDefault(ut => ut.TeamId == request.OlderTeamId.Value);
                if (olderUserTeam == null)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "Equipe anterior não encontrada no histórico do usuário."
                    });
                }

                if ((request.BoundaryDate - olderUserTeam.StartDate).TotalDays < 7)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "O período na equipe anterior deve ter duração mínima de 1 semana (7 dias)."
                    });
                }
            }

            if (request.NewerTeamId.HasValue)
            {
                newerUserTeam = userTeams.FirstOrDefault(ut => ut.TeamId == request.NewerTeamId.Value);
                if (newerUserTeam == null)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "Nova equipe não encontrada no histórico do usuário."
                    });
                }

                if (newerUserTeam.EndDate.HasValue && (newerUserTeam.EndDate.Value - request.BoundaryDate).TotalDays < 7)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "O período na nova equipe deve ter duração mínima de 1 semana (7 dias)."
                    });
                }
            }

            if (olderUserTeam != null)
            {
                olderUserTeam.EndDate = request.BoundaryDate.AddDays(-1);
                olderUserTeam.UpdatedAt = DateTime.UtcNow;
            }

            if (newerUserTeam != null)
            {
                newerUserTeam.StartDate = request.BoundaryDate;
                newerUserTeam.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Reload user history
            var updatedMemberships = await _teamRepository.GetAllMembershipsForUsersAsync(new[] { user.InternalId });
            var history = updatedMemberships.Select(m => new TeamCalendarUserHistoryItem
            {
                UserTeamId = m.Id,
                TeamId = m.TeamId,
                TeamName = m.Team?.Name ?? $"Equipe #{m.TeamId}",
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                IsActive = m.EndDate == null || m.EndDate > DateTime.UtcNow
            }).ToList();

            var activeTeam = history.FirstOrDefault(h => h.IsActive);

            var response = new TeamCalendarUserResponse
            {
                UserId = user.Id,
                UserInternalId = user.InternalId,
                UserName = user.Name,
                UserEmail = user.Email,
                CurrentTeamName = activeTeam?.TeamName,
                CurrentTeamId = activeTeam?.TeamId,
                HierarchyLevel = 1,
                ParentUserName = user.ParentUser?.Name,
                TeamHistory = history
            };

            return Ok(new ApiResponse<TeamCalendarUserResponse>
            {
                Success = true,
                Data = response,
                Message = "Datas atualizadas com sucesso."
            });
        }

        [HttpGet("calendar/available-teams")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<List<AvailableTeamItemResponse>>>> GetAvailableTeams()
        {
            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized();
            }

            HashSet<int>? allowedOwnerInternalIds = null;

            if (roleIdClaim != "1") // Not a Superadmin
            {
                var caller = await _userRepository.GetByIdAsync(currentUserId);
                if (caller == null)
                {
                    return Unauthorized();
                }

                var descendants = await _userHierarchyService.GetDescendantInternalIdsAsync(currentUserId);
                allowedOwnerInternalIds = new HashSet<int>(descendants) { caller.InternalId };
            }

            var teams = await _context.Teams
                .AsNoTracking()
                .Include(t => t.Store)
                .Include(t => t.Owner)
                .Include(t => t.UserTeams)
                .Where(t => allowedOwnerInternalIds == null ||
                            (t.OwnerUserInternalId.HasValue && allowedOwnerInternalIds.Contains(t.OwnerUserInternalId.Value)) ||
                            (!t.OwnerUserInternalId.HasValue && allowedOwnerInternalIds.Contains(t.UserTeams.Select(ut => ut.UserInternalId).FirstOrDefault())))
                .OrderBy(t => t.Name)
                .ToListAsync();

            var result = teams.Select(t => new AvailableTeamItemResponse
            {
                Id = t.Id,
                Name = t.Name,
                StoreName = t.Store?.Name,
                OwnerName = t.Owner?.Name,
                OwnerUserId = t.Owner?.Id,
                MemberCount = t.UserTeams.Count(ut => ut.EndDate == null || ut.EndDate > DateTime.UtcNow)
            }).ToList();

            return Ok(new ApiResponse<List<AvailableTeamItemResponse>>
            {
                Success = true,
                Data = result,
                Message = "Equipes disponíveis recuperadas com sucesso"
            });
        }

        [HttpPost("calendar/assign-team")]
        [HasPermission("teams:manage")]
        public async Task<ActionResult<ApiResponse<TeamCalendarUserResponse>>> AssignUserTeam([FromBody] AssignUserTeamRequest request)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new ApiResponse<TeamCalendarUserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            var roleIdClaim = User.FindFirst("role_id")?.Value;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (roleIdClaim == "2" && Guid.TryParse(userIdClaim, out var currentUserId))
            {
                var allowedUserIds = await _userHierarchyService.GetDescendantIdsAsync(currentUserId);
                if (!allowedUserIds.Contains(request.UserId))
                {
                    return Forbid();
                }
            }

            var targetTeam = await _teamRepository.GetByIdAsync(request.NewTeamId);
            if (targetTeam == null)
            {
                return NotFound(new ApiResponse<TeamCalendarUserResponse>
                {
                    Success = false,
                    Message = "Equipe de destino não encontrada."
                });
            }

            var userTeams = await _context.UserTeams
                .Where(ut => ut.UserInternalId == user.InternalId)
                .OrderBy(ut => ut.StartDate)
                .ToListAsync();

            // Check active team (where EndDate is null or > StartDate)
            var activeTeam = userTeams.FirstOrDefault(ut => ut.EndDate == null || ut.EndDate > request.StartDate);

            if (activeTeam != null)
            {
                if (activeTeam.TeamId == request.NewTeamId)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "O usuário já está ativo nesta equipe."
                    });
                }

                // Validate 1-week rule (7 days) on previous team
                if ((request.StartDate - activeTeam.StartDate).TotalDays < 7)
                {
                    return BadRequest(new ApiResponse<TeamCalendarUserResponse>
                    {
                        Success = false,
                        Message = "O período na equipe anterior deve ter duração mínima de 1 semana (7 dias)."
                    });
                }

                activeTeam.EndDate = request.StartDate.AddDays(-1);
                activeTeam.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // If this is the member's first team, ensure start date is 1 day before their earliest contract
                var earliestContract = await _context.Contracts
                    .AsNoTracking()
                    .Where(c => c.UserInternalId == user.InternalId && c.IsActive)
                    .OrderBy(c => c.SaleStartDate)
                    .FirstOrDefaultAsync();

                if (earliestContract != null)
                {
                    var oneDayBeforeContract = earliestContract.SaleStartDate.AddDays(-1);
                    if (request.StartDate > oneDayBeforeContract)
                    {
                        request.StartDate = oneDayBeforeContract;
                    }
                }
            }

            // Update parent user to the new team's owner if requested
            if (request.UpdateParentUser && targetTeam.OwnerUserInternalId.HasValue)
            {
                var targetOwner = await _context.Users.FirstOrDefaultAsync(u => u.InternalId == targetTeam.OwnerUserInternalId.Value && u.IsActive);
                if (targetOwner != null && targetOwner.Id != user.Id)
                {
                    // Check for circular hierarchy: ensure user.Id is not an ancestor of targetOwner.Id
                    var ownerAncestors = new HashSet<Guid>();
                    var currentParentId = targetOwner.ParentUserId;
                    int depth = 0;
                    while (currentParentId.HasValue && depth < 50)
                    {
                        if (currentParentId.Value == user.Id)
                        {
                            ownerAncestors.Add(user.Id);
                            break;
                        }
                        var parent = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentParentId.Value);
                        if (parent == null) break;
                        currentParentId = parent.ParentUserId;
                        depth++;
                    }

                    if (!ownerAncestors.Contains(user.Id))
                    {
                        user.ParentUserId = targetOwner.Id;
                        user.UpdatedAt = DateTime.UtcNow;
                        _context.Users.Update(user);
                    }
                }
            }

            // Add new UserTeam
            var newMembership = new UserTeam
            {
                TeamId = request.NewTeamId,
                UserInternalId = user.InternalId,
                StartDate = request.StartDate,
                EndDate = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserTeams.Add(newMembership);
            await _context.SaveChangesAsync();

            // Reload user history
            var updatedMemberships = await _teamRepository.GetAllMembershipsForUsersAsync(new[] { user.InternalId });
            var history = updatedMemberships.Select(m => new TeamCalendarUserHistoryItem
            {
                UserTeamId = m.Id,
                TeamId = m.TeamId,
                TeamName = m.Team?.Name ?? $"Equipe #{m.TeamId}",
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                IsActive = m.EndDate == null || m.EndDate > DateTime.UtcNow
            }).ToList();

            var currentActive = history.FirstOrDefault(h => h.IsActive);

            var response = new TeamCalendarUserResponse
            {
                UserId = user.Id,
                UserInternalId = user.InternalId,
                UserName = user.Name,
                UserEmail = user.Email,
                CurrentTeamName = currentActive?.TeamName,
                CurrentTeamId = currentActive?.TeamId,
                HierarchyLevel = 1,
                ParentUserName = user.ParentUser?.Name ?? (await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.ParentUserId))?.Name,
                TeamHistory = history
            };

            return Ok(new ApiResponse<TeamCalendarUserResponse>
            {
                Success = true,
                Data = response,
                Message = "Equipe atribuída com sucesso."
            });
        }
    }
}
