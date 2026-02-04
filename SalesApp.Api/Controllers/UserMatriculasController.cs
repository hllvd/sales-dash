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
    [Authorize(Roles = "admin,superadmin")]
    public class UserMatriculasController : ControllerBase
    {
        private readonly IUserMatriculaRepository _matriculaRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;

        public UserMatriculasController(
            IUserMatriculaRepository matriculaRepository,
            IUserRepository userRepository,
            IMessageService messageService)
        {
            _matriculaRepository = matriculaRepository;
            _userRepository = userRepository;
            _messageService = messageService;
        }

        // GET: api/usermatriculas
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserMatriculaResponse>>>> GetAll()
        {
            var matriculas = await _matriculaRepository.GetAllAsync();
            var responses = matriculas.Select(MapToResponse).ToList();

            return Ok(new ApiResponse<List<UserMatriculaResponse>>
            {
                Success = true,
                Data = responses,
                Message = _messageService.Get(AppMessage.MatriculasRetrievedSuccessfully)
            });
        }

        // GET: api/usermatriculas/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> GetById(int id)
        {
            var matricula = await _matriculaRepository.GetByIdAsync(id);
            
            if (matricula == null)
            {
                return NotFound(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaNotFound)
                });
            }

            return Ok(new ApiResponse<UserMatriculaResponse>
            {
                Success = true,
                Data = MapToResponse(matricula),
                Message = _messageService.Get(AppMessage.MatriculaRetrievedSuccessfully)
            });
        }

        // GET: api/usermatriculas/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<List<UserMatriculaResponse>>>> GetByUserId(Guid userId)
        {
            var matriculas = await _matriculaRepository.GetByUserIdAsync(userId);
            var responses = matriculas.Select(MapToResponse).ToList();

            return Ok(new ApiResponse<List<UserMatriculaResponse>>
            {
                Success = true,
                Data = responses,
                Message = _messageService.Get(AppMessage.MatriculasRetrievedSuccessfully)
            });
        }

        // POST: api/usermatriculas
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> Create(
            [FromBody] CreateUserMatriculaRequest request)
        {
            Guid userId;
            
            // Support both UserId and UserEmail
            if (request.UserId.HasValue)
            {
                userId = request.UserId.Value;
            }
            else if (!string.IsNullOrEmpty(request.UserEmail))
            {
                var user = await _userRepository.GetByEmailAsync(request.UserEmail);
                if (user == null)
                {
                    return NotFound(new ApiResponse<UserMatriculaResponse>
                    {
                        Success = false,
                        Message = $"User not found with email: {request.UserEmail}"
                    });
                }
                userId = user.Id;
            }
            else
            {
                return BadRequest(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = "Either UserId or UserEmail is required"
                });
            }
            
            // Verify user exists
            var existingUser = await _userRepository.GetByIdAsync(userId);
            if (existingUser == null)
            {
                return NotFound(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.UserNotFound)
                });
            }

            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaNumber = request.MatriculaNumber,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsOwner = request.IsOwner,
                IsActive = request.IsActive ?? true,
                Status = (request.Status ?? "active").ToLower()
            };

            try
            {
                var created = await _matriculaRepository.CreateAsync(matricula);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = created.Id },
                    new ApiResponse<UserMatriculaResponse>
                    {
                        Success = true,
                        Data = MapToResponse(created),
                        Message = _messageService.Get(AppMessage.MatriculaCreatedSuccessfully)
                    });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: api/usermatriculas/bulk
        [HttpPost("bulk")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<BulkCreateMatriculaResponse>>> BulkCreate(
            [FromBody] BulkCreateMatriculaRequest request)
        {
            var response = new BulkCreateMatriculaResponse();

            for (int i = 0; i < request.Matriculas.Count; i++)
            {
                var item = request.Matriculas[i];
                try
                {
                    Guid userId;
                    
                    // Lookup user by email or use provided UserId
                    if (item.UserId.HasValue)
                    {
                        userId = item.UserId.Value;
                    }
                    else if (!string.IsNullOrEmpty(item.UserEmail))
                    {
                        var user = await _userRepository.GetByEmailAsync(item.UserEmail);
                        if (user == null)
                        {
                            response.Errors.Add(new BulkImportError
                            {
                                RowNumber = i + 2, // +2 for header row and 0-index
                                MatriculaNumber = item.MatriculaNumber ?? "",
                                UserEmail = item.UserEmail,
                                Error = $"User not found with email: {item.UserEmail}"
                            });
                            continue;
                        }
                        userId = user.Id;
                    }
                    else
                    {
                        response.Errors.Add(new BulkImportError
                        {
                            RowNumber = i + 2,
                            MatriculaNumber = item.MatriculaNumber ?? "",
                            UserEmail = item.UserEmail ?? "",
                            Error = "Either UserId or UserEmail is required"
                        });
                        continue;
                    }

                    // Validate required fields
                    if (string.IsNullOrEmpty(item.MatriculaNumber))
                    {
                        response.Errors.Add(new BulkImportError
                        {
                            RowNumber = i + 2,
                            MatriculaNumber = "",
                            UserEmail = item.UserEmail ?? "",
                            Error = "MatriculaNumber is required"
                        });
                        continue;
                    }

                    // Create matricula
                    var matricula = new UserMatricula
                    {
                        UserId = userId,
                        MatriculaNumber = item.MatriculaNumber,
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                        IsOwner = item.IsOwner,
                        IsActive = item.IsActive ?? true,
                        Status = (item.Status ?? "active").ToLower()
                    };

                    var created = await _matriculaRepository.CreateAsync(matricula);
                    response.CreatedMatriculas.Add(MapToResponse(created));
                }
                catch (Exception ex)
                {
                    response.Errors.Add(new BulkImportError
                    {
                        RowNumber = i + 2,
                        MatriculaNumber = item.MatriculaNumber ?? "",
                        UserEmail = item.UserEmail ?? "",
                        Error = ex.Message
                    });
                }
            }

            response.TotalProcessed = request.Matriculas.Count;
            response.SuccessCount = response.CreatedMatriculas.Count;
            response.ErrorCount = response.Errors.Count;

            return Ok(new ApiResponse<BulkCreateMatriculaResponse>
            {
                Success = true,
                Data = response,
                Message = $"Processed {response.TotalProcessed} records: {response.SuccessCount} succeeded, {response.ErrorCount} failed"
            });
        }

        // PUT: api/usermatriculas/{id}

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> Update(
            int id,
            [FromBody] UpdateUserMatriculaRequest request)
        {
            var matricula = await _matriculaRepository.GetByIdAsync(id);
            
            if (matricula == null)
            {
                return NotFound(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaNotFound)
                });
            }

            if (request.MatriculaNumber != null)
                matricula.MatriculaNumber = request.MatriculaNumber;
            
            if (request.StartDate.HasValue)
                matricula.StartDate = request.StartDate.Value;
            
            if (request.EndDate.HasValue)
                matricula.EndDate = request.EndDate;
            
            if (request.IsActive.HasValue)
                matricula.IsActive = request.IsActive.Value;
            
            if (request.IsOwner.HasValue)
                matricula.IsOwner = request.IsOwner.Value;
            
            if (request.Status != null)
                matricula.Status = request.Status.ToLower();

            try
            {
                var updated = await _matriculaRepository.UpdateAsync(matricula);

                return Ok(new ApiResponse<UserMatriculaResponse>
                {
                    Success = true,
                    Data = MapToResponse(updated),
                    Message = _messageService.Get(AppMessage.MatriculaUpdatedSuccessfully)
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // DELETE: api/usermatriculas/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            var matricula = await _matriculaRepository.GetByIdAsync(id);
            
            if (matricula == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaNotFound)
                });
            }

            await _matriculaRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.MatriculaDeletedSuccessfully)
            });
        }

        // POST: api/usermatriculas/bulk-assign
        [HttpPost("bulk-assign")]
        public async Task<ActionResult<ApiResponse<BulkAssignResult>>> BulkAssign(
            [FromBody] BulkAssignMatriculasRequest request)
        {
            var result = new BulkAssignResult
            {
                TotalProcessed = request.Assignments.Count,
                Created = new List<UserMatriculaResponse>(),
                Errors = new List<string>()
            };

            foreach (var assignment in request.Assignments)
            {
                try
                {
                    // Verify user exists
                    var user = await _userRepository.GetByIdAsync(assignment.UserId);
                    if (user == null)
                    {
                        result.Errors.Add($"User {assignment.UserId} not found for matricula {assignment.MatriculaNumber}");
                        continue;
                    }

                    // Check if matricula already exists for this user
                    var existing = await _matriculaRepository.GetByUserIdAsync(assignment.UserId);
                    if (existing.Any(m => m.MatriculaNumber == assignment.MatriculaNumber))
                    {
                        result.Errors.Add($"Matricula {assignment.MatriculaNumber} already exists for user {user.Name}");
                        continue;
                    }

                    // Create new matricula
                    var matricula = new UserMatricula
                    {
                        UserId = assignment.UserId,
                        MatriculaNumber = assignment.MatriculaNumber,
                        StartDate = assignment.StartDate,
                        IsActive = true
                    };

                    var created = await _matriculaRepository.CreateAsync(matricula);
                    result.Created.Add(MapToResponse(created));
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error processing matricula {assignment.MatriculaNumber}: {ex.Message}");
                }
            }

            result.SuccessCount = result.Created.Count;
            result.ErrorCount = result.Errors.Count;

            return Ok(new ApiResponse<BulkAssignResult>
            {
                Success = true,
                Data = result,
                Message = $"Bulk assign completed: {result.SuccessCount} created, {result.ErrorCount} errors"
            });
        }

        private UserMatriculaResponse MapToResponse(UserMatricula matricula)
        {
            return new UserMatriculaResponse
            {
                Id = matricula.Id,
                UserId = matricula.UserId,
                UserName = matricula.User?.Name ?? "",
                MatriculaNumber = matricula.MatriculaNumber,
                StartDate = matricula.StartDate,
                EndDate = matricula.EndDate,
                IsActive = matricula.IsActive,
                IsOwner = matricula.IsOwner,
                Status = matricula.Status,
                CreatedAt = matricula.CreatedAt
            };
        }
    }
}
