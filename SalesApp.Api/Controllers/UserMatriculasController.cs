using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;
using SalesApp.Utils;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserMatriculasController : ControllerBase
    {
        private readonly IUserMatriculaRepository _userMatriculaRepository;
        private readonly IMatriculaRepository _matriculaRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;

        public UserMatriculasController(
            IUserMatriculaRepository userMatriculaRepository,
            IMatriculaRepository matriculaRepository,
            IUserRepository userRepository,
            IMessageService messageService)
        {
            _userMatriculaRepository = userMatriculaRepository;
            _matriculaRepository = matriculaRepository;
            _userRepository = userRepository;
            _messageService = messageService;
        }

        // GET: api/usermatriculas
        [HttpGet]
        [HasPermission("matriculas:read")]
        public async Task<ActionResult<ApiResponse<List<UserMatriculaResponse>>>> GetAll()
        {
            var matriculas = await _userMatriculaRepository.GetAllAsync();
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
        [HasPermission("matriculas:read")]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> GetById(int id)
        {
            var matricula = await _userMatriculaRepository.GetByIdAsync(id);
            
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
        [HasPermission("matriculas:read")]
        public async Task<ActionResult<ApiResponse<List<UserMatriculaResponse>>>> GetByUserId(Guid userId)
        {
            var matriculas = await _userMatriculaRepository.GetByUserIdAsync(userId);
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
        [HasPermission("matriculas:write")]
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

            // 1. Ensure the Matricula exists
            var normalizedNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            var matriculaEntity = await _matriculaRepository.GetByMatriculaNumberAsync(normalizedNumber);
            if (matriculaEntity == null)
            {
                matriculaEntity = new Matricula
                {
                    MatriculaNumber = normalizedNumber,
                    StartDate = request.StartDate,
                    Status = (request.Status ?? "active").ToLower()
                };
                await _matriculaRepository.CreateAsync(matriculaEntity);
            }

            var userMatricula = new UserMatricula
            {
                UserInternalId = existingUser.InternalId,
                MatriculaId = matriculaEntity.Id,
                EndDate = request.EndDate,
                IsOwner = request.IsOwner,
                IsActive = request.IsActive ?? true
            };

            try
            {
                var created = await _userMatriculaRepository.CreateAsync(userMatricula);

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
        [HasPermission("matriculas:write")]
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

                    // 1. Ensure the Matricula exists
                    var normalizedNumber = NormalizationUtils.NormalizeNumber(item.MatriculaNumber);
                    var matriculaEntity = await _matriculaRepository.GetByMatriculaNumberAsync(normalizedNumber);
                    if (matriculaEntity == null)
                    {
                        matriculaEntity = new Matricula
                        {
                            MatriculaNumber = normalizedNumber,
                            StartDate = item.StartDate,
                            Status = (item.Status ?? "active").ToLower()
                        };
                        await _matriculaRepository.CreateAsync(matriculaEntity);
                    }

                    // Create user-matricula link
                    var userMatricula = new UserMatricula
                    {
                        UserInternalId = (await _userRepository.GetByIdAsync(userId))!.InternalId,
                        MatriculaId = matriculaEntity.Id,
                        EndDate = item.EndDate,
                        IsOwner = item.IsOwner,
                        IsActive = item.IsActive ?? true
                    };

                    var created = await _userMatriculaRepository.CreateAsync(userMatricula);
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
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> Update(
            int id,
            [FromBody] UpdateUserMatriculaRequest request)
        {
            var userMatricula = await _userMatriculaRepository.GetByIdAsync(id);
            
            if (userMatricula == null)
            {
                return NotFound(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaNotFound)
                });
            }

            // Handle Matricula entity updates
            if (userMatricula.Matricula != null)
            {
                bool matriculaChanged = false;

                if (!string.IsNullOrEmpty(request.MatriculaNumber))
                {
                    var normalizedNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
                    if (userMatricula.Matricula.MatriculaNumber != normalizedNumber)
                    {
                        // Check if another matricula with this number exists
                        var existingMatricula = await _matriculaRepository.GetByMatriculaNumberAsync(normalizedNumber);
                        if (existingMatricula != null)
                        {
                            // Link to existing one
                            userMatricula.MatriculaId = existingMatricula.Id;
                            userMatricula.Matricula = existingMatricula;
                        }
                        else
                        {
                            // Rename current one
                            userMatricula.Matricula.MatriculaNumber = normalizedNumber;
                            matriculaChanged = true;
                        }
                    }
                }

                if (request.StartDate.HasValue && userMatricula.Matricula.StartDate != request.StartDate.Value)
                {
                    userMatricula.Matricula.StartDate = request.StartDate.Value;
                    matriculaChanged = true;
                }

                if (!string.IsNullOrEmpty(request.Status) && userMatricula.Matricula.Status != request.Status.ToLower())
                {
                    userMatricula.Matricula.Status = request.Status.ToLower();
                    matriculaChanged = true;
                }

                if (matriculaChanged)
                {
                    await _matriculaRepository.UpdateAsync(userMatricula.Matricula);
                }
            }

            if (request.EndDate.HasValue)
                userMatricula.EndDate = request.EndDate;
            
            if (request.IsActive.HasValue)
                userMatricula.IsActive = request.IsActive.Value;
            
            if (request.IsOwner.HasValue)
                userMatricula.IsOwner = request.IsOwner.Value;

            try
            {
                var updated = await _userMatriculaRepository.UpdateAsync(userMatricula);

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
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            var matricula = await _userMatriculaRepository.GetByIdAsync(id);
            
            if (matricula == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.MatriculaNotFound)
                });
            }

            await _userMatriculaRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.MatriculaDeletedSuccessfully)
            });
        }

        // POST: api/usermatriculas/bulk-assign
        [HttpPost("bulk-assign")]
        [HasPermission("matriculas:write")]
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
                    var existing = await _userMatriculaRepository.GetByUserIdAsync(assignment.UserId);
                    if (existing.Any(m => m.Matricula != null && m.Matricula.MatriculaNumber == assignment.MatriculaNumber))
                    {
                        result.Errors.Add($"Matricula {assignment.MatriculaNumber} already exists for user {user.Name}");
                        continue;
                    }

                    // 1. Ensure the Matricula exists
                    var normalizedNumber = NormalizationUtils.NormalizeNumber(assignment.MatriculaNumber);
                    var matriculaEntity = await _matriculaRepository.GetByMatriculaNumberAsync(normalizedNumber);
                    if (matriculaEntity == null)
                    {
                        matriculaEntity = new Matricula
                        {
                            MatriculaNumber = normalizedNumber,
                            StartDate = assignment.StartDate,
                            Status = "active"
                        };
                        await _matriculaRepository.CreateAsync(matriculaEntity);
                    }

                    // Create link
                    var userMatricula = new UserMatricula
                    {
                        UserInternalId = user.InternalId,
                        MatriculaId = matriculaEntity.Id,
                        IsActive = true
                    };

                    var created = await _userMatriculaRepository.CreateAsync(userMatricula);
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
                UserId = matricula.User?.Id ?? Guid.Empty,
                UserName = matricula.User?.Name ?? "",
                MatriculaNumber = matricula.Matricula?.MatriculaNumber ?? "",
                StartDate = matricula.Matricula?.StartDate ?? DateTime.MinValue,
                EndDate = matricula.EndDate,
                IsActive = matricula.IsActive,
                IsOwner = matricula.IsOwner,
                Status = matricula.Matricula?.Status ?? "",
                CreatedAt = matricula.CreatedAt
            };
        }
    }
}
