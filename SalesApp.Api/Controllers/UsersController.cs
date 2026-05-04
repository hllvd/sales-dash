using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Data;
using SalesApp.Attributes;
using SalesApp.Utils;
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
        private readonly IUserMatriculaRepository _userMatriculaRepository;
        private readonly IMatriculaRepository _matriculaRepository;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IMessageService _messageService;
        private readonly IEmailService _emailService;
        private readonly IExportService _exportService;
        
        public UsersController(
            IUserRepository userRepository,
            IJwtService jwtService,
            IUserHierarchyService hierarchyService,
            IContractRepository contractRepository,
            IRoleRepository roleRepository,
            IUserMatriculaRepository userMatriculaRepository,
            IMatriculaRepository matriculaRepository,
            IConfiguration configuration,
            AppDbContext context,
            IMessageService messageService,
            IEmailService emailService,
            IExportService exportService)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _hierarchyService = hierarchyService;
            _contractRepository = contractRepository;
            _roleRepository = roleRepository;
            _userMatriculaRepository = userMatriculaRepository;
            _matriculaRepository = matriculaRepository;
            _configuration = configuration;
            _context = context;
            _messageService = messageService;
            _emailService = emailService;
            _exportService = exportService;
        }

        // ── My-Contracts Export endpoints ────────────────────────────────────
        
        /// <summary>
        /// Queue an async XLSX export scoped strictly to the current user's own contracts.
        /// </summary>
        [HttpPost("me/contracts/export")]
        [Authorize]
        public ActionResult<ApiResponse<ExportJobResponse>> StartMyExport([FromBody] ContractExportRequest request)
        {
            var currentUserId = GetCurrentUserId();
            // Scope: only this user's contracts
            var scope = new Models.UserScopeContext
            {
                IsGlobal = false,
                AllowedUserIds = new HashSet<Guid> { currentUserId }
            };
            // Force filters to current user only — ignore any userId override from body
            request.UserId = currentUserId;
            var requestingUserId = currentUserId.ToString();
            var jobId = _exportService.StartExport(request, scope, requestingUserId);
            var status = _exportService.GetJobStatus(jobId)!;
            return Ok(new ApiResponse<ExportJobResponse> { Success = true, Data = status, Message = "Export started" });
        }

        [HttpGet("me/contracts/export/{jobId}")]
        [Authorize]
        public ActionResult<ApiResponse<ExportJobResponse>> GetMyExportStatus(string jobId)
        {
            var status = _exportService.GetJobStatus(jobId);
            if (status == null)
                return NotFound(new ApiResponse<ExportJobResponse> { Success = false, Message = "Job not found or expired" });
            return Ok(new ApiResponse<ExportJobResponse> { Success = true, Data = status, Message = "OK" });
        }

        [HttpGet("me/contracts/export/{jobId}/download")]
        [Authorize]
        public ActionResult DownloadMyExport(string jobId)
        {
            var requestingUserId = GetCurrentUserId().ToString();
            var bytes = _exportService.GetJobBytes(jobId, requestingUserId);
            if (bytes == null)
                return NotFound(new { success = false, message = "File not ready, expired, or not found" });

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"meus_contratos_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }


        
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> Register(RegisterRequest request)
        {
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            // Normalize email to lowercase
            request.Email = request.Email.ToLowerInvariant().Trim();
            
            if (!UserRole.IsValid(request.Role))
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.InvalidRole)
                });
            }
            
            if (await _userRepository.EmailExistsAsync(request.Email))
            {
                return BadRequest(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.EmailAlreadyExists)
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
                    Message = _messageService.Get(AppMessage.InvalidRole)
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
                    // 1. Ensure the Matricula exists
                    var matriculaEntity = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
                    if (matriculaEntity == null)
                    {
                        matriculaEntity = new Matricula
                        {
                            MatriculaNumber = request.MatriculaNumber,
                            StartDate = DateTime.UtcNow,
                            Status = "active"
                        };
                        await _matriculaRepository.CreateAsync(matriculaEntity);
                    }
                    
                    bool isOwner = request.IsMatriculaOwner;
                    
                    // Link user to matricula
                    var userMatricula = new UserMatricula
                    {
                        UserId = user.Id,
                        MatriculaId = matriculaEntity.Id,
                        IsOwner = isOwner,
                        IsActive = true
                    };
                    
                    await _userMatriculaRepository.CreateAsync(userMatricula);
                }
                catch (Exception ex)
                {
                    // Log error but don't fail user creation
                    // User was created successfully, matricula assignment failed
                    return Ok(new ApiResponse<UserResponse>
                    {
                        Success = true,
                        Data = MapToUserResponse(user),
                        Message = _messageService.Get(AppMessage.UserCreatedButMatriculaFailed, ex.Message)
                    });
                }
            }
            
            // Send welcome email if requested
            if (request.SendEmail)
            {
                try
                {
                    await _emailService.SendWelcomeEmailAsync(user.Email, user.Name, request.Password);
                }
                catch (Exception)
                {
                    // Email sending failed, but user was created successfully
                    // Don't fail the registration
                }
            }
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = _messageService.Get(AppMessage.UserCreatedSuccessfully)
            });
        }
        
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request)
        {
            // Normalize email to lowercase
            var email = request.Email.ToLowerInvariant().Trim();
            var user = await _userRepository.GetByEmailAsync(email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.InvalidCredentials)
                });
            }
            
            var token = await _jwtService.GenerateToken(user);
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
                Message = _messageService.Get(AppMessage.LoginSuccessful)
            });
        }
        
        [HttpGet]
        [HasPermission("users:read")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserResponse>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? contractNumber = null)
        {
            var (users, totalCount) = await _userRepository.GetAllAsync(page, pageSize, search, contractNumber);
            
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
                Message = _messageService.Get(AppMessage.UsersRetrievedSuccessfully)
            });
        }
        
        [HttpGet("role/{roleId}")]
        [HasPermission("users:read")]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetUsersByRole(int roleId)
        {
            var users = await _userRepository.GetByRoleIdAsync(roleId);
            
            return Ok(new ApiResponse<List<UserResponse>>
            {
                Success = true,
                Data = users.Select(MapToUserResponse).ToList(),
                Message = _messageService.Get(AppMessage.UsersRetrievedSuccessfully)
            });
        }

        [HttpGet("by-matricula/{matricula}")]
        [HttpGet("lookup/{matricula}")]
        [HasPermission("users:read")]
        public ActionResult<ApiResponse<List<UserLookupResponse>>> LookupByMatricula(string matricula)
        {
            // This endpoint is deprecated - use UserMatriculas endpoints instead
            return Ok(new ApiResponse<List<UserLookupResponse>>
            {
                Success = false,
                Message = _messageService.Get(AppMessage.EndpointDeprecated),
                Data = new List<UserLookupResponse>()
            });
        }
        
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetUser(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var hasReadPermission = User.HasClaim("perm", "users:read") || User.HasClaim("perm", "system:superadmin");
            
            if (!hasReadPermission && currentUserId != id)
            {
                return Forbid();
            }
            
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = MapToUserResponse(user),
                Message = _messageService.Get(AppMessage.UserRetrievedSuccessfully)
            });
        }
        
        [HttpPut("{id}")]
        [HasPermission("users:profile-update")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(Guid id, UpdateUserRequest request)
        {
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            var currentUserId = GetCurrentUserId();
            var hasUpdatePermission = User.HasClaim("perm", "users:update") || User.HasClaim("perm", "system:superadmin");
            
            // Ownership check: users can only update themselves unless they have broader update permissions
            if (!hasUpdatePermission && currentUserId != id)
            {
                return Forbid();
            }
            
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }
            
            if (!string.IsNullOrEmpty(request.Name))
                user.Name = request.Name;
                
            if (!string.IsNullOrEmpty(request.Email))
            {
                // Normalize email to lowercase
                request.Email = request.Email.ToLowerInvariant().Trim();
                
                if (await _userRepository.EmailExistsAsync(request.Email, id))
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.EmailAlreadyExists)
                    });
                }
                user.Email = request.Email;
            }
            
            if (!string.IsNullOrEmpty(request.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                
            if (!string.IsNullOrEmpty(request.Role) && hasUpdatePermission)
            {
                if (!UserRole.IsValid(request.Role))
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.InvalidRole)
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
            if (request.ParentUserId.HasValue && hasUpdatePermission)
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
            
            if (request.IsActive.HasValue && hasUpdatePermission)
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
                Message = _messageService.Get(AppMessage.UserUpdatedSuccessfully)
            });
        }
        
        [HttpDelete("{id}")]
        [HasPermission("users:delete")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }
            
            user.IsActive = false;
            await _userRepository.UpdateAsync(user);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.UserDeletedSuccessfully)
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
                MatriculaId = primaryMatricula?.MatriculaId,
                MatriculaNumber = primaryMatricula?.Matricula?.MatriculaNumber,
                IsMatriculaOwner = primaryMatricula != null,

                // Active matriculas for current user
                ActiveMatriculas = user.UserMatriculas?
                    .Where(m => m.IsActive)
                    .OrderByDescending(m => m.IsOwner)
                    .Select(m => new UserMatriculaInfo
                    {
                        Id = m.Id,
                        MatriculaId = m.MatriculaId,
                        MatriculaNumber = m.Matricula?.MatriculaNumber ?? "",
                        IsOwner = m.IsOwner,
                        Status = m.Matricula?.Status ?? "active",
                        StartDate = m.Matricula?.StartDate ?? DateTime.MinValue,
                        EndDate = m.EndDate
                    })
                    .ToList() ?? new List<UserMatriculaInfo>(),
                
            };
        }
        
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
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
                Message = _messageService.Get(AppMessage.ParentRetrievedSuccessfully)
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
                Message = _messageService.Get(AppMessage.ChildrenRetrievedSuccessfully)
            });
        }
        
        [HttpGet("{id}/tree")]
        [HasPermission("users:read")]
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
                Message = _messageService.Get(AppMessage.TreeRetrievedSuccessfully)
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
                Message = _messageService.Get(AppMessage.LevelRetrievedSuccessfully)
            });
        }
        
        [HttpGet("root")]
        [HasPermission("users:read")]
        public async Task<ActionResult<ApiResponse<UserHierarchyResponse?>>> GetRoot()
        {
            var root = await _hierarchyService.GetRootUserAsync();
            
            return Ok(new ApiResponse<UserHierarchyResponse?>
            {
                Success = true,
                Data = root != null ? MapToHierarchyResponse(root) : null,
                Message = _messageService.Get(AppMessage.RootUserRetrievedSuccessfully)
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
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }
            
            // Get active matriculas for current user
            var activeMatriculas = await _userMatriculaRepository.GetActiveByUserIdAsync(currentUserId);
            
            var userResponse = MapToUserResponse(user);
            userResponse.ActiveMatriculas = activeMatriculas
                .Select(m => new UserMatriculaInfo
                {
                    Id = m.Id,
                    MatriculaId = m.MatriculaId,
                    MatriculaNumber = m.Matricula?.MatriculaNumber ?? "",
                    IsOwner = m.IsOwner,
                    Status = m.Matricula?.Status ?? "active",
                    StartDate = m.Matricula?.StartDate ?? DateTime.MinValue,
                    EndDate = m.EndDate
                })
                .ToList();
            
            return Ok(new ApiResponse<UserResponse>
            {
                Success = true,
                Data = userResponse,
                Message = _messageService.Get(AppMessage.CurrentUserRetrievedSuccessfully)
            });
        }
        
        [HttpPost("me/request-matricula")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserMatriculaInfo>>> RequestMatricula([FromBody] RequestMatriculaRequest request)
        {
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            var currentUserId = GetCurrentUserId();
            
            // Check if user already has this matricula
            var existingMatricula = await _userMatriculaRepository.GetByMatriculaNumberAndUserIdAsync(request.MatriculaNumber, currentUserId);
            if (existingMatricula != null)
            {
                return BadRequest(new ApiResponse<UserMatriculaInfo>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaAlreadyAssigned)
                });
            }
            
            // 1. Ensure the Matricula exists
            var matriculaEntity = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
            if (matriculaEntity == null)
            {
                matriculaEntity = new Matricula
                {
                    MatriculaNumber = request.MatriculaNumber,
                    StartDate = DateTime.UtcNow,
                    Status = MatriculaStatus.Pending.ToApiString() // Or appropriate default
                };
                await _matriculaRepository.CreateAsync(matriculaEntity);
            }

            // Create new matricula request with pending status
            var userMatricula = new UserMatricula
            {
                UserId = currentUserId,
                MatriculaId = matriculaEntity.Id,
                IsActive = false, // Not active until approved
                IsOwner = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.UserMatriculas.Add(userMatricula);
            await _context.SaveChangesAsync();
            
            return Ok(new ApiResponse<UserMatriculaInfo>
            {
                Success = true,
                Data = new UserMatriculaInfo
                {
                    Id = userMatricula.Id,
                    MatriculaNumber = matriculaEntity.MatriculaNumber,
                    IsOwner = userMatricula.IsOwner,
                    Status = matriculaEntity.Status,
                    StartDate = matriculaEntity.StartDate,
                    EndDate = userMatricula.EndDate
                },
                Message = _messageService.Get(AppMessage.MatriculaRequestSubmitted)
            });
        }
        
        [HttpPost("{id}/reset-password")]
        [HasPermission("users:reset-password")]
        public async Task<ActionResult<ApiResponse<ResetPasswordResponse>>> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
        {
            var currentUserId = GetCurrentUserId();
            
            // Prevent users from resetting their own password via this endpoint
            if (currentUserId == id)
            {
                return BadRequest(new ApiResponse<ResetPasswordResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.CannotResetOwnPassword)
                });
            }
            
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<ResetPasswordResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }
            
            // Generate new password using user's name
            var newPassword = PasswordGenerator.GeneratePassword(user.Name);
            
            // Update user's password in database
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            
            // Optionally send email with new password
            bool emailSent = false;
            if (request.SendEmail)
            {
                emailSent = await _emailService.SendPasswordResetEmailAsync(
                    user.Email,
                    user.Name,
                    newPassword
                );
            }
            
            // Return response with new password
            var message = emailSent 
                ? _messageService.Get(AppMessage.PasswordResetWithEmailSent)
                : _messageService.Get(AppMessage.PasswordResetSuccessfully);
            
            return Ok(new ApiResponse<ResetPasswordResponse>
            {
                Success = true,
                Data = new ResetPasswordResponse
                {
                    NewPassword = newPassword,
                    EmailSent = emailSent
                },
                Message = message
            });
        }
        
        [HttpPost("assign-contract/{contractNumber}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> AssignContract(
            string contractNumber,
            [FromQuery] string? matriculaNumber = null,
            [FromQuery] int? userMatriculaId = null)
        {
            contractNumber = NormalizationUtils.NormalizeNumber(contractNumber);
            matriculaNumber = NormalizationUtils.NormalizeNumber(matriculaNumber);
            var currentUserId = GetCurrentUserId();
            
            // Validate and get matricula if provided
            UserMatricula? userMatricula = null;
            if (userMatriculaId.HasValue)
            {
                userMatricula = await _userMatriculaRepository.GetByIdAsync(userMatriculaId.Value);
                if (userMatricula == null || userMatricula.UserId != currentUserId)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.MatriculaDoesNotBelongToUser)
                    });
                }
            }
            else if (!string.IsNullOrEmpty(matriculaNumber))
            {
                userMatricula = await _userMatriculaRepository.GetByMatriculaNumberAndUserIdAsync(matriculaNumber, currentUserId);
                
                if (userMatricula == null)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.MatriculaDoesNotBelongToUser)
                    });
                }
            }

            if (userMatricula != null)
            {
                if (!userMatricula.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.MatriculaNotActive)
                    });
                }
                
                // Check if matricula is still valid (not expired)
                if (userMatricula.EndDate.HasValue && userMatricula.EndDate.Value < DateTime.UtcNow)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.MatriculaExpired)
                    });
                }
            }
            
            var contract = await _contractRepository.GetByContractNumberAsync(contractNumber);
            if (contract == null)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ContractNotFound)
                });
            }
            
            var user = await _userRepository.GetByIdAsync(currentUserId);
            if (user == null)
            {
                return Unauthorized(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            contract.UserId = currentUserId;
            contract.User = user;
            contract.TempMatricula = matriculaNumber;
            contract.MatriculaId = userMatricula?.MatriculaId;
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
                    ContractType = ContractTypeExtensions.ToApiString(contract.ContractType),
                    Quota = contract.Quota,
                    PvId = contract.PvId,
                    CustomerName = contract.CustomerName,
                    MatriculaId = userMatricula?.MatriculaId,
                    MatriculaNumber = userMatricula?.Matricula?.MatriculaNumber
                },
                Message = _messageService.Get(AppMessage.ContractAssignedSuccessfully)
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