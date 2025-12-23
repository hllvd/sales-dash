using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Data;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IUserHierarchyService _hierarchyService;
        private readonly IContractRepository _contractRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserMatriculaRepository _matriculaRepository;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        
        public UsersController(
            IUserRepository userRepository, 
            IJwtService jwtService, 
            IUserHierarchyService hierarchyService, 
            IContractRepository contractRepository,
            IRoleRepository roleRepository,
            IUserMatriculaRepository matriculaRepository,
            IConfiguration configuration,
            AppDbContext context)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _hierarchyService = hierarchyService;
            _contractRepository = contractRepository;
            _roleRepository = roleRepository;
            _matriculaRepository = matriculaRepository;
            _configuration = configuration;
            _context = context;
        }
        
        [HttpPost("register")]
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
            
            // Validate hierarchy rules
            var hierarchyError = await _hierarchyService.ValidateHierarchyChangeAsync(Guid.NewGuid(), request.ParentUserId);
            if (hierarchyError != null)
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = hierarchyError
                });
            }

            var role = await _roleRepository.GetByNameAsync(request.Role);
            if (role == null)
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "Invalid role specified."
                });
            }
            
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = role.Id,
                ParentUserId = request.ParentUserId,
                IsActive = true,
                Level = 1 // Default level for new users, will be adjusted by hierarchy service if needed
            };
            
            try
            {
                await _userRepository.CreateAsync(user);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            
            // Handle matricula assignment if provided
            if (!string.IsNullOrEmpty(request.MatriculaNumber))
            {
                try
                {
                    // Check if matricula already exists
                    var existingMatricula = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
                    
                    bool isOwner = request.IsMatriculaOwner;
                    
                    // If matricula doesn't exist and user wants to be owner, they become the owner
                    if (existingMatricula == null)
                    {
                        isOwner = true; // First user with this matricula becomes owner
                    }
                    
                    var userMatricula = new UserMatricula
                    {
                        UserId = user.Id,
                        MatriculaNumber = request.MatriculaNumber,
                        StartDate = DateTime.UtcNow,
                        IsOwner = isOwner,
                        IsActive = true
                    };
                    
                    await _matriculaRepository.CreateAsync(userMatricula);
                }
                catch (Exception ex)
                {
                    // Log error but don't fail user creation
                    // User was created successfully, matricula assignment failed
                    return Ok(new ApiResponse<UserResponse>
                    {
                        Success = true,
                        Data = MapToUserResponse(user),
                        Message = $"User created successfully, but matricula assignment failed: {ex.Message}"
                    });
                }
            }
            
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
            var refreshToken = _jwtService.GenerateRefreshToken();
            
            // Get refresh token expiration from config
            var refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
            var expiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            
            // Store refresh token in database
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();
            
            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Data = new LoginResponse
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    User = MapToUserResponse(user)
                },
                Message = "Login successful"
            });
        }
        
        [HttpGet]
        [Authorize(Roles = "superadmin")]
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
        
        [HttpGet("role/{roleId}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetUsersByRole(int roleId)
        {
            var users = await _userRepository.GetByRoleIdAsync(roleId);
            
            return Ok(new ApiResponse<List<UserResponse>>
            {
                Success = true,
                Data = users.Select(MapToUserResponse).ToList(),
                Message = "Users retrieved successfully"
            });
        }

        [HttpGet("by-matricula/{matricula}")]
        [HttpGet("lookup/{matricula}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<UserLookupResponse>>>> LookupByMatricula(string matricula)
        {
            // This endpoint is deprecated - use UserMatriculas endpoints instead
            return Ok(new ApiResponse<List<UserLookupResponse>>
            {
                Success = false,
                Message = "This endpoint is deprecated. Please use /api/usermatriculas endpoints to manage matriculas.",
                Data = new List<UserLookupResponse>()
            });
        }
        
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,superadmin,user")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetUser(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserRole != "superadmin" && currentUserId != id)
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(Guid id, UpdateUserRequest request)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
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
                // Role validation will be handled by RoleId in future update
                var role = await _roleRepository.GetByNameAsync(request.Role);
                if (role != null)
                {
                    user.RoleId = role.Id;
                }
            }
                
            // Handle parent user assignment
            if (request.ParentUserId.HasValue && (currentUserRole == "admin" || currentUserRole == "superadmin"))
            {
                var hierarchyError = await _hierarchyService.ValidateHierarchyChangeAsync(id, request.ParentUserId);
                if (hierarchyError != null)
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = hierarchyError
                    });
                }
                user.ParentUserId = request.ParentUserId;
            }
            
            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }
                
            try
            {
                await _userRepository.UpdateAsync(user);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = "User updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "superadmin")]
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
            // Get the primary/owner matricula if it exists
            var primaryMatricula = user.UserMatriculas?.FirstOrDefault(m => m.IsOwner && m.IsActive);
            
            return new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role?.Name ?? "",
                ParentUserId = user.ParentUserId,
                ParentUserName = user.ParentUser?.Name,
                IsActive = user.IsActive,

                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                
                // Matricula information
                MatriculaId = primaryMatricula?.Id,
                MatriculaNumber = primaryMatricula?.MatriculaNumber,
                IsMatriculaOwner = primaryMatricula != null
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
        
        [HttpGet("{id}/parent")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserHierarchyResponse?>>> GetParent(Guid id)
        {
            var parent = await _hierarchyService.GetParentAsync(id);
            
            return Ok(new ApiResponse<UserHierarchyResponse?>
            {
                Success = true,
                Data = parent != null ? MapToHierarchyResponse(parent) : null,
                Message = "Parent retrieved successfully"
            });
        }
        
        [HttpGet("{id}/children")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<UserHierarchyResponse>>>> GetChildren(Guid id)
        {
            var children = await _hierarchyService.GetChildrenAsync(id);
            
            return Ok(new ApiResponse<List<UserHierarchyResponse>>
            {
                Success = true,
                Data = children.Select(MapToHierarchyResponse).ToList(),
                Message = "Children retrieved successfully"
            });
        }
        
        [HttpGet("{id}/tree")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<UserTreeResponse>>> GetTree(Guid id, [FromQuery] int depth = -1)
        {
            var tree = await _hierarchyService.GetTreeAsync(id, depth);
            
            return Ok(new ApiResponse<UserTreeResponse>
            {
                Success = true,
                Data = new UserTreeResponse
                {
                    Users = tree.Select(MapToHierarchyResponse).ToList(),
                    TotalUsers = tree.Count,
                    MaxDepth = tree.Any() ? tree.Max(u => u.Level) : 0
                },
                Message = "Tree retrieved successfully"
            });
        }
        
        [HttpGet("{id}/level")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<int>>> GetLevel(Guid id)
        {
            var level = await _hierarchyService.GetLevelAsync(id);
            
            return Ok(new ApiResponse<int>
            {
                Success = true,
                Data = level,
                Message = "Level retrieved successfully"
            });
        }
        
        [HttpGet("root")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<UserHierarchyResponse?>>> GetRoot()
        {
            var root = await _hierarchyService.GetRootUserAsync();
            
            return Ok(new ApiResponse<UserHierarchyResponse?>
            {
                Success = true,
                Data = root != null ? MapToHierarchyResponse(root) : null,
                Message = "Root user retrieved successfully"
            });
        }
        
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetCurrentUser()
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userRepository.GetByIdAsync(currentUserId);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            
            // Get active matriculas for current user
            var activeMatriculas = await _matriculaRepository.GetActiveByUserIdAsync(currentUserId);
            
            var userResponse = MapToUserResponse(user);
            userResponse.ActiveMatriculas = activeMatriculas
                .Select(m => new UserMatriculaInfo
                {
                    Id = m.Id,
                    MatriculaNumber = m.MatriculaNumber,
                    IsOwner = m.IsOwner,
                    StartDate = m.StartDate,
                    EndDate = m.EndDate
                })
                .ToList();
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = userResponse,
                Message = "Current user retrieved successfully"
            });
        }
        
        [HttpPost("assign-contract/{contractNumber}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> AssignContract(
            string contractNumber,
            [FromQuery] string? matriculaNumber = null)
        {
            var currentUserId = GetCurrentUserId();
            
            // Validate and get matricula if provided
            UserMatricula? userMatricula = null;
            if (!string.IsNullOrEmpty(matriculaNumber))
            {
                userMatricula = await _matriculaRepository.GetByMatriculaNumberAndUserIdAsync(matriculaNumber, currentUserId);
                
                if (userMatricula == null)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Matricula not found or doesn't belong to you"
                    });
                }
                
                if (!userMatricula.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Matricula is not active"
                    });
                }
                
                // Check if matricula is still valid (not expired)
                if (userMatricula.EndDate.HasValue && userMatricula.EndDate.Value < DateTime.UtcNow)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Matricula has expired"
                    });
                }
            }
            
            var contract = await _contractRepository.GetByContractNumberAsync(contractNumber);
            if (contract == null)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "Contract not found"
                });
            }
            
            var user = await _userRepository.GetByIdAsync(currentUserId);
            if (user == null)
            {
                return Unauthorized(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            contract.UserId = currentUserId;
            contract.User = user;
            contract.MatriculaId = userMatricula?.Id;
            contract.UserMatricula = userMatricula;
            await _contractRepository.UpdateAsync(contract);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = new ContractResponse
                {
                    Id = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    UserId = currentUserId,
                    UserName = user.Name,
                    TotalAmount = contract.TotalAmount,
                    GroupId = contract.GroupId,
                    GroupName = contract.Group?.Name ?? "",
                    Status = contract.Status,
                    ContractStartDate = contract.SaleStartDate,
                    IsActive = contract.IsActive,
                    CreatedAt = contract.CreatedAt,
                    UpdatedAt = contract.UpdatedAt,
                    ContractType = contract.ContractType,
                    Quota = contract.Quota,
                    PvId = contract.PvId,
                    CustomerName = contract.CustomerName,
                    MatriculaNumber = userMatricula?.MatriculaNumber
                },
                Message = "Contract assigned successfully"
            });
        }

        private UserHierarchyResponse MapToHierarchyResponse(User user)
        {
            return new UserHierarchyResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role?.Name ?? "",
                ParentUserId = user.ParentUserId,
                ParentUserName = user.ParentUser?.Name,
                Level = user.Level,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}