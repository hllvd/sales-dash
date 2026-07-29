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

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClassificationsController : ControllerBase
    {
        private readonly IClassificationLevelRepository _levelRepo;
        private readonly IUserClassificationRepository _userClassRepo;
        private readonly IUserRepository _userRepo;
        private readonly IMessageService _messageService;

        public ClassificationsController(
            IClassificationLevelRepository levelRepo,
            IUserClassificationRepository userClassRepo,
            IUserRepository userRepo,
            IMessageService messageService)
        {
            _levelRepo = levelRepo;
            _userClassRepo = userClassRepo;
            _userRepo = userRepo;
            _messageService = messageService;
        }

        // ── Level CRUD ───────────────────────────────────────────────────────────
        
        [HttpGet("levels")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ClassificationLevelResponse>>>> GetLevels()
        {
            var levels = await _levelRepo.GetAllAsync();
            var responses = new List<ClassificationLevelResponse>();
            foreach (var l in levels)
            {
                var count = await _levelRepo.GetActiveUsersCountAsync(l.Id);
                responses.Add(MapToLevelResponse(l, count));
            }
            return Ok(new ApiResponse<List<ClassificationLevelResponse>>
            {
                Success = true,
                Data = responses,
                Message = _messageService.Get(AppMessage.ClassificationLevelsRetrievedSuccessfully)
            });
        }

        [HttpGet("levels/{id}")]
        public async Task<ActionResult<ApiResponse<ClassificationLevelResponse>>> GetLevel(int id)
        {
            var level = await _levelRepo.GetByIdAsync(id);
            if (level == null)
                return NotFound(new ApiResponse<ClassificationLevelResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNotFound)
                });

            var count = await _levelRepo.GetActiveUsersCountAsync(id);
            return Ok(new ApiResponse<ClassificationLevelResponse>
            {
                Success = true,
                Data = MapToLevelResponse(level, count),
                Message = _messageService.Get(AppMessage.ClassificationLevelRetrievedSuccessfully)
            });
        }

        [HttpPost("levels")]
        [Authorize(Roles = "SuperAdmin,superadmin")]
        public async Task<ActionResult<ApiResponse<ClassificationLevelResponse>>> CreateLevel(CreateClassificationLevelRequest request)
        {
            if (await _levelRepo.NameExistsAsync(request.Name))
                return BadRequest(new ApiResponse<ClassificationLevelResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNameAlreadyExists)
                });

            var level = new ClassificationLevel
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Prize = request.Prize?.Trim(),
                SalesGoal = request.SalesGoal,
                Retention = request.Retention,
                NextLevelId = request.NextLevelId,
                MinimumDirect1LevelId = request.MinimumDirect1LevelId,
                MinimumDirect1MinCount = request.MinimumDirect1MinCount,
                MinimumDirect2LevelId = request.MinimumDirect2LevelId,
                MinimumDirect2MinCount = request.MinimumDirect2MinCount
            };

            var created = await _levelRepo.CreateAsync(level);
            // Reload with navigation properties (NextLevel, MinimumDirect levels)
            var createdWithNav = await _levelRepo.GetByIdAsync(created.Id);
            return Ok(new ApiResponse<ClassificationLevelResponse>
            {
                Success = true,
                Data = MapToLevelResponse(createdWithNav!, 0),
                Message = _messageService.Get(AppMessage.ClassificationLevelCreatedSuccessfully)
            });
        }

        [HttpPut("levels/{id}")]
        [Authorize(Roles = "SuperAdmin,superadmin")]
        public async Task<ActionResult<ApiResponse<ClassificationLevelResponse>>> UpdateLevel(int id, UpdateClassificationLevelRequest request)
        {
            var level = await _levelRepo.GetByIdAsync(id);
            if (level == null)
                return NotFound(new ApiResponse<ClassificationLevelResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNotFound)
                });

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().ToLower() != level.Name.ToLower())
            {
                if (await _levelRepo.NameExistsAsync(request.Name, id))
                    return BadRequest(new ApiResponse<ClassificationLevelResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.ClassificationLevelNameAlreadyExists)
                    });
                level.Name = request.Name.Trim();
            }

            if (request.Description != null) level.Description = request.Description.Trim();
            if (request.Prize != null) level.Prize = request.Prize.Trim();
            if (request.SalesGoal.HasValue) level.SalesGoal = request.SalesGoal;
            if (request.Retention.HasValue) level.Retention = request.Retention;

            // NextLevel: explicit clear flag takes priority over new value
            if (request.ClearNextLevel)
                level.NextLevelId = null;
            else if (request.NextLevelId.HasValue)
                level.NextLevelId = request.NextLevelId;

            // MinimumDirect #1
            if (request.ClearMinimumDirect1)
            {
                level.MinimumDirect1LevelId = null;
                level.MinimumDirect1MinCount = null;
            }
            else
            {
                if (request.MinimumDirect1LevelId.HasValue) level.MinimumDirect1LevelId = request.MinimumDirect1LevelId;
                if (request.MinimumDirect1MinCount.HasValue) level.MinimumDirect1MinCount = request.MinimumDirect1MinCount;
            }

            // MinimumDirect #2
            if (request.ClearMinimumDirect2)
            {
                level.MinimumDirect2LevelId = null;
                level.MinimumDirect2MinCount = null;
            }
            else
            {
                if (request.MinimumDirect2LevelId.HasValue) level.MinimumDirect2LevelId = request.MinimumDirect2LevelId;
                if (request.MinimumDirect2MinCount.HasValue) level.MinimumDirect2MinCount = request.MinimumDirect2MinCount;
            }

            await _levelRepo.UpdateAsync(level);
            // Reload with navigation properties so NextLevelName etc. are populated
            var reloaded = await _levelRepo.GetByIdAsync(id);
            var count = await _levelRepo.GetActiveUsersCountAsync(id);
            return Ok(new ApiResponse<ClassificationLevelResponse>
            {
                Success = true,
                Data = MapToLevelResponse(reloaded!, count),
                Message = _messageService.Get(AppMessage.ClassificationLevelUpdatedSuccessfully)
            });
        }

        [HttpDelete("levels/{id}")]
        [Authorize(Roles = "SuperAdmin,superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteLevel(int id)
        {
            var level = await _levelRepo.GetByIdAsync(id);
            if (level == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNotFound)
                });

            var activeCount = await _levelRepo.GetActiveUsersCountAsync(id);
            if (activeCount > 0)
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelHasActiveUsers)
                });

            await _levelRepo.DeleteAsync(id);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.ClassificationLevelDeletedSuccessfully)
            });
        }

        [HttpGet("levels/{levelId}/members")]
        public async Task<ActionResult<ApiResponse<List<UserClassificationResponse>>>> GetLevelMembers(int levelId)
        {
            var level = await _levelRepo.GetByIdAsync(levelId);
            if (level == null)
                return NotFound(new ApiResponse<List<UserClassificationResponse>>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNotFound)
                });

            var assignments = await _userClassRepo.GetForLevelAsync(levelId);
            var responses = new List<UserClassificationResponse>();
            foreach (var uc in assignments)
            {
                if (uc.User != null)
                {
                    responses.Add(MapToUserClassResponse(uc, uc.User));
                }
            }

            return Ok(new ApiResponse<List<UserClassificationResponse>>
            {
                Success = true,
                Data = responses,
                Message = "Membros do nível de classificação recuperados com sucesso."
            });
        }

        // ── User–Level Assignments ───────────────────────────────────────────────

        [HttpPost("assign")]
        public async Task<ActionResult<ApiResponse<UserClassificationResponse>>> AssignLevel(AssignUserLevelRequest request)
        {
            var user = await _userRepo.GetByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiResponse<UserClassificationResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });

            var level = await _levelRepo.GetByIdAsync(request.LevelId);
            if (level == null)
                return NotFound(new ApiResponse<UserClassificationResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ClassificationLevelNotFound)
                });

            if (request.EndDate.HasValue && request.StartDate > request.EndDate.Value)
                return BadRequest(new ApiResponse<UserClassificationResponse>
                {
                    Success = false,
                    Message = "A data de início deve ser anterior à data de fim."
                });

            // Auto-close any current active assignment
            var current = await _userClassRepo.GetActiveForUserAsync(user.InternalId);
            string? overlapMessage = null;
            if (current != null)
            {
                current.EndDate = request.StartDate;
                await _userClassRepo.UpdateAsync(current);
                overlapMessage = _messageService.Get(AppMessage.UserClassificationOverlapConflict);
            }

            var assignment = new UserClassification
            {
                UserInternalId = user.InternalId,
                LevelId = request.LevelId,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var created = await _userClassRepo.CreateAsync(assignment);

            // Reload with navigation props
            var reloaded = await _userClassRepo.GetByIdAsync(created.Id);
            return Ok(new ApiResponse<UserClassificationResponse>
            {
                Success = true,
                Data = MapToUserClassResponse(reloaded!, user),
                Message = overlapMessage ?? _messageService.Get(AppMessage.UserClassificationAssignedSuccessfully)
            });
        }

        [HttpGet("users/{userId}/history")]
        public async Task<ActionResult<ApiResponse<List<UserClassificationResponse>>>> GetUserHistory(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiResponse<List<UserClassificationResponse>>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });

            var history = await _userClassRepo.GetForUserAsync(user.InternalId);
            var responses = history.Select(uc => MapToUserClassResponse(uc, user)).ToList();
            return Ok(new ApiResponse<List<UserClassificationResponse>>
            {
                Success = true,
                Data = responses,
                Message = _messageService.Get(AppMessage.UserClassificationHistoryRetrievedSuccessfully)
            });
        }

        [HttpGet("users/{userId}/active")]
        public async Task<ActionResult<ApiResponse<UserClassificationResponse?>>> GetUserActiveLevel(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiResponse<UserClassificationResponse?>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });

            var active = await _userClassRepo.GetActiveForUserAsync(user.InternalId);
            return Ok(new ApiResponse<UserClassificationResponse?>
            {
                Success = true,
                Data = active != null ? MapToUserClassResponse(active, user) : null,
                Message = _messageService.Get(AppMessage.ClassificationLevelRetrievedSuccessfully)
            });
        }

        [HttpPut("assignments/{id}")]
        public async Task<ActionResult<ApiResponse<UserClassificationResponse>>> UpdateAssignment(int id, UpdateUserClassificationDatesRequest request)
        {
            var assignment = await _userClassRepo.GetByIdAsync(id);
            if (assignment == null)
                return NotFound(new ApiResponse<UserClassificationResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserClassificationNotFound)
                });

            if (request.EndDate.HasValue && request.StartDate > request.EndDate.Value)
                return BadRequest(new ApiResponse<UserClassificationResponse>
                {
                    Success = false,
                    Message = "A data de início deve ser anterior à data de fim."
                });

            assignment.StartDate = request.StartDate;
            assignment.EndDate = request.EndDate;
            await _userClassRepo.UpdateAsync(assignment);

            var reloaded = await _userClassRepo.GetByIdAsync(id);
            var user = await _userRepo.GetByIdAsync(reloaded!.User.Id);
            return Ok(new ApiResponse<UserClassificationResponse>
            {
                Success = true,
                Data = MapToUserClassResponse(reloaded, user!),
                Message = _messageService.Get(AppMessage.UserClassificationUpdatedSuccessfully)
            });
        }

        [HttpDelete("assignments/{id}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveAssignment(int id)
        {
            var assignment = await _userClassRepo.GetByIdAsync(id);
            if (assignment == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserClassificationNotFound)
                });

            // Soft-remove by setting EndDate to now
            assignment.EndDate = DateTime.UtcNow;
            await _userClassRepo.UpdateAsync(assignment);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.UserClassificationUpdatedSuccessfully)
            });
        }

        // ── Mappers ──────────────────────────────────────────────────────────────

        private static ClassificationLevelResponse MapToLevelResponse(ClassificationLevel l, int activeCount) =>
            new ClassificationLevelResponse
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                Prize = l.Prize,
                SalesGoal = l.SalesGoal,
                Retention = l.Retention,
                NextLevelId = l.NextLevelId,
                NextLevelName = l.NextLevel?.Name,
                MinimumDirect1LevelId = l.MinimumDirect1LevelId,
                MinimumDirect1LevelName = l.MinimumDirect1Level?.Name,
                MinimumDirect1MinCount = l.MinimumDirect1MinCount,
                MinimumDirect2LevelId = l.MinimumDirect2LevelId,
                MinimumDirect2LevelName = l.MinimumDirect2Level?.Name,
                MinimumDirect2MinCount = l.MinimumDirect2MinCount,
                ActiveUsersCount = activeCount,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            };

        private static UserClassificationResponse MapToUserClassResponse(UserClassification uc, User user) =>
            new UserClassificationResponse
            {
                Id = uc.Id,
                UserId = user.Id,
                UserInternalId = user.InternalId,
                UserName = user.Name,
                UserEmail = user.Email,
                LevelId = uc.LevelId,
                LevelName = uc.Level?.Name ?? "",
                LevelDescription = uc.Level?.Description,
                LevelPrize = uc.Level?.Prize,
                LevelSalesGoal = uc.Level?.SalesGoal,
                StartDate = uc.StartDate,
                EndDate = uc.EndDate,
                IsActive = uc.EndDate == null || uc.EndDate > DateTime.UtcNow,
                CreatedAt = uc.CreatedAt
            };
    }
}
