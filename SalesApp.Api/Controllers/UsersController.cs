using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using System.Security.Claims;
using BCrypt.Net;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        
        public UsersController(IUserRepository userRepository, IJwtService jwtService)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
        }
        
        [HttpPost("register")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> Register(RegisterRequest request)
        {
            if (!UserRole.IsValid(request.Role))
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "Invalid role. Must be: user, admin, or superadmin"
                });
            }
            
            if (await _userRepository.EmailExistsAsync(request.Email))
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "Email already exists"
                });
            }
            
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role
            };
            
            await _userRepository.CreateAsync(user);
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = "User created successfully"
            });
        }
        
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "Invalid credentials"
                });
            }
            
            var token = _jwtService.GenerateToken(user);
            
            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Data = new LoginResponse
                {
                    Token = token,
                    User = MapToUserResponse(user)
                },
                Message = "Login successful"
            });
        }
        
        [HttpGet]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserResponse>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var (users, totalCount) = await _userRepository.GetAllAsync(page, pageSize, search);
            
            return Ok(new ApiResponse<PagedResponse<UserResponse>>
            {
                Success = true,
                Data = new PagedResponse<UserResponse>
                {
                    Items = users.Select(MapToUserResponse).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                },
                Message = "Users retrieved successfully"
            });
        }
        
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetUser(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserId != id)
            {
                return Forbid();
            }
            
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = "User retrieved successfully"
            });
        }
        
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(Guid id, UpdateUserRequest request)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserId != id)
            {
                return Forbid();
            }
            
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            
            if (!string.IsNullOrEmpty(request.Name))
                user.Name = request.Name;
                
            if (!string.IsNullOrEmpty(request.Email))
            {
                if (await _userRepository.EmailExistsAsync(request.Email, id))
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = "Email already exists"
                    });
                }
                user.Email = request.Email;
            }
            
            if (!string.IsNullOrEmpty(request.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                
            if (!string.IsNullOrEmpty(request.Role) && (currentUserRole == "admin" || currentUserRole == "superadmin"))
            {
                if (!UserRole.IsValid(request.Role))
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = "Invalid role. Must be: user, admin, or superadmin"
                    });
                }
                user.Role = request.Role;
            }
                
            if (request.IsActive.HasValue && (currentUserRole == "admin" || currentUserRole == "superadmin"))
                user.IsActive = request.IsActive.Value;
                
            await _userRepository.UpdateAsync(user);
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = "User updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            
            user.IsActive = false;
            await _userRepository.UpdateAsync(user);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "User deleted successfully"
            });
        }
        
        private UserResponse MapToUserResponse(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
        
        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.User;
        }
    }
}