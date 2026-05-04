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
    public class MatriculasController : ControllerBase
    {
        private readonly IMatriculaRepository _matriculaRepository;
        private readonly IMessageService _messageService;

        public MatriculasController(
            IMatriculaRepository matriculaRepository,
            IMessageService messageService)
        {
            _matriculaRepository = matriculaRepository;
            _messageService = messageService;
        }

        // GET: api/matriculas
        [HttpGet]
        [HasPermission("matriculas:read")]
        public async Task<ActionResult<ApiResponse<List<Matricula>>>> GetAll()
        {
            var matriculas = (await _matriculaRepository.GetAllAsync()).ToList();

            return Ok(new ApiResponse<List<Matricula>>
            {
                Success = true,
                Data = matriculas,
                Message = "Matriculas retrieved successfully"
            });
        }

        // GET: api/matriculas/{id}
        [HttpGet("{id}")]
        [HasPermission("matriculas:read")]
        public async Task<ActionResult<ApiResponse<Matricula>>> GetById(int id)
        {
            var matricula = await _matriculaRepository.GetByIdAsync(id);
            
            if (matricula == null)
            {
                return NotFound(new ApiResponse<Matricula>
                {
                    Success = false,
                    Message = "Matricula not found"
                });
            }

            return Ok(new ApiResponse<Matricula>
            {
                Success = true,
                Data = matricula,
                Message = "Matricula retrieved successfully"
            });
        }

        // POST: api/matriculas
        [HttpPost]
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<MatriculaResponse>>> Create(MatriculaRequest request)
        {
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            
            if (await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber) != null)
            {
                return BadRequest(new ApiResponse<MatriculaResponse>
                {
                    Success = false,
                    Message = "Matricula number already exists"
                });
            }

            var matricula = new Matricula
            {
                MatriculaNumber = request.MatriculaNumber,
                Status = request.Status ?? "active",
                StartDate = request.StartDate ?? DateTime.UtcNow
            };

            var created = await _matriculaRepository.CreateAsync(matricula);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                new ApiResponse<MatriculaResponse>
                {
                    Success = true,
                    Data = MapToMatriculaResponse(created),
                    Message = "Matricula created successfully"
                });
        }

        // PUT: api/matriculas/{id}
        [HttpPut("{id}")]
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<MatriculaResponse>>> Update(int id, MatriculaRequest request)
        {
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
            
            var existing = await _matriculaRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<MatriculaResponse>
                {
                    Success = false,
                    Message = "Matricula not found"
                });
            }

            if (!string.IsNullOrEmpty(request.MatriculaNumber))
            {
                var other = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
                if (other != null && other.Id != id)
                {
                    return BadRequest(new ApiResponse<MatriculaResponse>
                    {
                        Success = false,
                        Message = "Matricula number already exists"
                    });
                }
                existing.MatriculaNumber = request.MatriculaNumber;
            }

            if (!string.IsNullOrEmpty(request.Status))
                existing.Status = request.Status;
                
            if (request.StartDate.HasValue)
                existing.StartDate = request.StartDate.Value;

            var updated = await _matriculaRepository.UpdateAsync(existing);

            return Ok(new ApiResponse<MatriculaResponse>
            {
                Success = true,
                Data = MapToMatriculaResponse(updated),
                Message = "Matricula updated successfully"
            });
        }

        // DELETE: api/matriculas/{id}
        [HttpDelete("{id}")]
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _matriculaRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Matricula deleted successfully"
            });
        }

        private MatriculaResponse MapToMatriculaResponse(Matricula matricula)
        {
            return new MatriculaResponse
            {
                Id = matricula.Id,
                MatriculaNumber = matricula.MatriculaNumber,
                Status = matricula.Status,
                StartDate = matricula.StartDate
            };
        }
    }
}
