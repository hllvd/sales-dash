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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<GroupResponse>>>> GetGroups()
        {
            var groups = await _groupRepository.GetAllAsync();
            
            return Ok(new ApiResponse<List<GroupResponse>>
            {
                Success = true,
                Data = groups.Select(MapToGroupResponse).ToList(),
                Message = _messageService.Get(AppMessage.GroupsRetrievedSuccessfully)
            });
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> GetGroup(int id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || !group.IsActive)
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
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> CreateGroup(GroupRequest request)
        {
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
                Commission = request.Commission
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<GroupResponse>>> UpdateGroup(int id, UpdateGroupRequest request)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || !group.IsActive)
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
            
            if (!string.IsNullOrEmpty(request.Description))
                group.Description = request.Description;
                
            if (request.Commission.HasValue)
                group.Commission = request.Commission.Value;
                
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
        [Authorize(Roles = "superadmin")]
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
        
        private GroupResponse MapToGroupResponse(Group group)
        {
            return new GroupResponse
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Commission = group.Commission,
                IsActive = group.IsActive,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }
    }
}