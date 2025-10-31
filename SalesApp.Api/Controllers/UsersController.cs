using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Services;
using System.Security.Claims;
using BCrypt.Net;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        
        public UsersController(AppDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }
        
        [HttpPost("register")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> Register(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
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
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
            
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
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserResponse>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var query = _context.Users.AsQueryable();
            
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
            }
            
            var totalCount = await query.CountAsync();
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
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
            
            var user = await _context.Users.FindAsync(id);
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
            
            var user = await _context.Users.FindAsync(id);
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
                if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
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
                
            if (!string.IsNullOrEmpty(request.Role) && currentUserRole == "admin")
                user.Role = request.Role;
                
            if (request.IsActive.HasValue && currentUserRole == "admin")
                user.IsActive = request.IsActive.Value;
                
            user.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = "User updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
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
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "normal";
        }
    }
}