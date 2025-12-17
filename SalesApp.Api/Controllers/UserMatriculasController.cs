using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,superadmin")]
    public class UserMatriculasController : ControllerBase
    {
        private readonly IUserMatriculaRepository _matriculaRepository;
        private readonly IUserRepository _userRepository;

        public UserMatriculasController(
            IUserMatriculaRepository matriculaRepository,
            IUserRepository userRepository)
        {
            _matriculaRepository = matriculaRepository;
            _userRepository = userRepository;
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
                Message = "User matriculas retrieved successfully"
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
                    Message = "User matricula not found"
                });
            }

            return Ok(new ApiResponse<UserMatriculaResponse>
            {
                Success = true,
                Data = MapToResponse(matricula),
                Message = "User matricula retrieved successfully"
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
                Message = "User matriculas retrieved successfully"
            });
        }

        // POST: api/usermatriculas
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserMatriculaResponse>>> Create(
            [FromBody] CreateUserMatriculaRequest request)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserMatriculaResponse>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var matricula = new UserMatricula
            {
                UserId = request.UserId,
                MatriculaNumber = request.MatriculaNumber,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true
            };

            var created = await _matriculaRepository.CreateAsync(matricula);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                new ApiResponse<UserMatriculaResponse>
                {
                    Success = true,
                    Data = MapToResponse(created),
                    Message = "User matricula created successfully"
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
                    Message = "User matricula not found"
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

            var updated = await _matriculaRepository.UpdateAsync(matricula);

            return Ok(new ApiResponse<UserMatriculaResponse>
            {
                Success = true,
                Data = MapToResponse(updated),
                Message = "User matricula updated successfully"
            });
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
                    Message = "User matricula not found"
                });
            }

            await _matriculaRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "User matricula deleted successfully"
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
                UserName = matricula.User?.Name ?? string.Empty,
                MatriculaNumber = matricula.MatriculaNumber,
                StartDate = matricula.StartDate,
                EndDate = matricula.EndDate,
                IsActive = matricula.IsActive,
                CreatedAt = matricula.CreatedAt
            };
        }
    }
}
