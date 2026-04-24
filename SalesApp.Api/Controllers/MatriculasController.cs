using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;

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
        public async Task<ActionResult<ApiResponse<Matricula>>> Create([FromBody] Matricula matricula)
        {
            var created = await _matriculaRepository.CreateAsync(matricula);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                new ApiResponse<Matricula>
                {
                    Success = true,
                    Data = created,
                    Message = "Matricula created successfully"
                });
        }

        // PUT: api/matriculas/{id}
        [HttpPut("{id}")]
        [HasPermission("matriculas:write")]
        public async Task<ActionResult<ApiResponse<Matricula>>> Update(int id, [FromBody] Matricula matricula)
        {
            if (id != matricula.Id)
            {
                return BadRequest(new ApiResponse<Matricula>
                {
                    Success = false,
                    Message = "ID mismatch"
                });
            }

            var updated = await _matriculaRepository.UpdateAsync(matricula);

            return Ok(new ApiResponse<Matricula>
            {
                Success = true,
                Data = updated,
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
    }
}
