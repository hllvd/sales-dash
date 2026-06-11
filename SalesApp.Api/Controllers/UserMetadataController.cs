using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Attributes;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserMetadataController : ControllerBase
    {
        private readonly IUserMetadataRepository _userMetadataRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserHierarchyService _hierarchyService;
        private readonly IMessageService _messageService;

        public UserMetadataController(
            IUserMetadataRepository userMetadataRepository,
            IUserRepository userRepository,
            IUserHierarchyService hierarchyService,
            IMessageService messageService)
        {
            _userMetadataRepository = userMetadataRepository;
            _userRepository = userRepository;
            _hierarchyService = hierarchyService;
            _messageService = messageService;
        }

        // ==========================================
        // FIELD DEFINITIONS (Superadmin Only)
        // ==========================================

        [HttpGet("fields")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<UserMetadataFieldResponse>>>> GetFields()
        {
            // Anyone authenticated can read active fields, but only superadmin sees all (including inactive ones)
            var isSuperadmin = User.HasClaim("perm", "system:superadmin") || User.FindFirst("role_id")?.Value == "1";
            
            var fields = isSuperadmin 
                ? await _userMetadataRepository.GetAllFieldsAsync()
                : await _userMetadataRepository.GetActiveFieldsAsync();

            var response = fields.Select(MapToFieldResponse).ToList();

            return Ok(new ApiResponse<List<UserMetadataFieldResponse>>
            {
                Success = true,
                Data = response,
                Message = "Fields retrieved successfully"
            });
        }

        [HttpPost("fields")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserMetadataFieldResponse>>> CreateField(
            [FromBody] UserMetadataFieldRequest request)
        {
            if (!IsSuperadmin())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Label))
            {
                return BadRequest(new ApiResponse<UserMetadataFieldResponse>
                {
                    Success = false,
                    Message = "Key and Label are required."
                });
            }

            // Verify uniqueness of Key
            var existing = await _userMetadataRepository.GetFieldByKeyAsync(request.Key.Trim());
            if (existing != null)
            {
                return BadRequest(new ApiResponse<UserMetadataFieldResponse>
                {
                    Success = false,
                    Message = $"A field with the key '{request.Key}' already exists."
                });
            }

            var field = new UserMetadataField
            {
                Key = request.Key.Trim(),
                Label = request.Label.Trim(),
                GroupLabel = request.GroupLabel?.Trim(),
                FieldType = request.FieldType ?? "text",
                DropdownOptions = request.DropdownOptions,
                DisplayOrder = request.DisplayOrder,
                IsRequired = request.IsRequired,
                IsActive = true
            };

            var created = await _userMetadataRepository.CreateFieldAsync(field);

            return CreatedAtAction(
                null,
                new ApiResponse<UserMetadataFieldResponse>
                {
                    Success = true,
                    Data = MapToFieldResponse(created),
                    Message = "Field created successfully"
                });
        }

        [HttpPut("fields/{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserMetadataFieldResponse>>> UpdateField(
            int id,
            [FromBody] UserMetadataFieldRequest request)
        {
            if (!IsSuperadmin())
            {
                return Forbid();
            }

            var field = await _userMetadataRepository.GetFieldByIdAsync(id);
            if (field == null)
            {
                return NotFound(new ApiResponse<UserMetadataFieldResponse>
                {
                    Success = false,
                    Message = "Field definition not found."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Label))
            {
                return BadRequest(new ApiResponse<UserMetadataFieldResponse>
                {
                    Success = false,
                    Message = "Key and Label are required."
                });
            }

            // Verify uniqueness of Key if it has changed
            if (field.Key != request.Key.Trim())
            {
                var existing = await _userMetadataRepository.GetFieldByKeyAsync(request.Key.Trim());
                if (existing != null)
                {
                    return BadRequest(new ApiResponse<UserMetadataFieldResponse>
                    {
                        Success = false,
                        Message = $"A field with the key '{request.Key}' already exists."
                    });
                }
            }

            field.Key = request.Key.Trim();
            field.Label = request.Label.Trim();
            field.GroupLabel = request.GroupLabel?.Trim();
            field.FieldType = request.FieldType ?? "text";
            field.DropdownOptions = request.DropdownOptions;
            field.DisplayOrder = request.DisplayOrder;
            field.IsRequired = request.IsRequired;

            var updated = await _userMetadataRepository.UpdateFieldAsync(field);

            return Ok(new ApiResponse<UserMetadataFieldResponse>
            {
                Success = true,
                Data = MapToFieldResponse(updated),
                Message = "Field updated successfully"
            });
        }

        [HttpDelete("fields/{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteField(int id)
        {
            if (!IsSuperadmin())
            {
                return Forbid();
            }

            var deleted = await _userMetadataRepository.DeleteFieldAsync(id);
            if (!deleted)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Field definition not found."
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Field definition soft-deleted successfully"
            });
        }

        // ==========================================
        // USER METADATA VALUES (Hierarchical Access)
        // ==========================================

        [HttpGet("{userId}/values")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<UserMetadataGroupDto>>>> GetValues(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<List<UserMetadataGroupDto>>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (!await CanAccessUserMetadata(userId))
            {
                return Forbid();
            }

            var activeFields = await _userMetadataRepository.GetActiveFieldsAsync();
            var userValues = await _userMetadataRepository.GetValuesByUserInternalIdAsync(user.InternalId);
            var valuesMap = userValues.ToDictionary(v => v.UserMetadataFieldId, v => v.Value);

            // Group fields by GroupLabel
            var groupedDtos = activeFields
                .GroupBy(f => f.GroupLabel)
                .Select(g => new UserMetadataGroupDto(
                    g.Key,
                    g.Select(f => new UserMetadataFieldValueDto(
                        f.Id,
                        f.Key,
                        f.Label,
                        f.FieldType,
                        f.DropdownOptions,
                        f.IsRequired,
                        valuesMap.TryGetValue(f.Id, out var val) ? val : null
                    )).ToList()
                ))
                .OrderBy(g => g.GroupLabel == null ? 1 : 0) // groups first, standalone last
                .ThenBy(g => g.GroupLabel)
                .ToList();

            return Ok(new ApiResponse<List<UserMetadataGroupDto>>
            {
                Success = true,
                Data = groupedDtos,
                Message = "User metadata values retrieved successfully"
            });
        }

        [HttpPut("{userId}/values")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> UpdateValues(
            Guid userId,
            [FromBody] UpsertUserMetadataRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (!await CanAccessUserMetadata(userId))
            {
                return Forbid();
            }

            // Validate required fields
            var activeFields = await _userMetadataRepository.GetActiveFieldsAsync();
            var requestValuesMap = request.Values.ToDictionary(v => v.FieldId, v => v.Value);

            foreach (var field in activeFields)
            {
                if (field.IsRequired)
                {
                    if (requestValuesMap.TryGetValue(field.Id, out var val))
                    {
                        if (string.IsNullOrWhiteSpace(val))
                        {
                            return BadRequest(new ApiResponse<object>
                            {
                                Success = false,
                                Message = $"O campo '{field.Label}' é obrigatório."
                            });
                        }
                    }
                }
            }

            await _userMetadataRepository.UpsertValuesAsync(user.InternalId, request.Values);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "User metadata values updated successfully"
            });
        }

        // ==========================================
        // PRIVATE HELPERS
        // ==========================================

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private bool IsSuperadmin()
        {
            return User.HasClaim("perm", "system:superadmin") || User.FindFirst("role_id")?.Value == "1";
        }

        private async Task<bool> CanAccessUserMetadata(Guid targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            
            // 1. Superadmin can access anyone
            if (IsSuperadmin()) return true;

            // 2. Own user can access
            if (currentUserId == targetUserId) return true;

            // 3. Admin can access their descendants
            var isAdmin = User.HasClaim("perm", "users:update") || User.FindFirst("role_id")?.Value == "2";
            if (isAdmin)
            {
                var descendants = await _hierarchyService.GetDescendantIdsAsync(currentUserId);
                return descendants.Contains(targetUserId);
            }

            return false;
        }

        private UserMetadataFieldResponse MapToFieldResponse(UserMetadataField field)
        {
            return new UserMetadataFieldResponse(
                field.Id,
                field.Key,
                field.Label,
                field.GroupLabel,
                field.FieldType,
                field.DropdownOptions,
                field.DisplayOrder,
                field.IsRequired,
                field.IsActive
            );
        }
    }
}
