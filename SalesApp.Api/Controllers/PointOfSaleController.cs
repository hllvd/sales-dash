using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/point-of-sale")]
    public class PointOfSaleController : ControllerBase
    {
        private readonly IPVRepository _pvRepository;
        private readonly IMessageService _messageService;
        
        public PointOfSaleController(IPVRepository pvRepository, IMessageService messageService)
        {
            _pvRepository = pvRepository;
            _messageService = messageService;
        }
        
        [HttpGet]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<List<PVResponse>>>> GetAll()
        {
            var pvs = await _pvRepository.GetAllAsync();
            var response = pvs.Select(p => new PVResponse
            {
                Id = p.Id,
                Name = p.Name,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                MatriculaId = p.MatriculaId
            }).ToList();
            
            return Ok(new ApiResponse<List<PVResponse>>
            {
                Success = true,
                Data = response,
                Message = _messageService.Get(AppMessage.PVsRetrievedSuccessfully)
            });
        }
        
        [HttpGet("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> GetById(int id)
        {
            var pv = await _pvRepository.GetByIdAsync(id);
            
            if (pv == null)
            {
                return NotFound(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.PVNotFound)
                });
            }
            
            var response = new PVResponse
            {
                Id = pv.Id,
                Name = pv.Name,
                CreatedAt = pv.CreatedAt,
                UpdatedAt = pv.UpdatedAt,
                MatriculaId = pv.MatriculaId
            };
            
            return Ok(new ApiResponse<PVResponse>
            {
                Success = true,
                Data = response,
                Message = _messageService.Get(AppMessage.PVRetrievedSuccessfully)
            });
        }
        
        [HttpPost]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> Create(PVRequest request)
        {
            // Check if ID already exists
            if (await _pvRepository.ExistsAsync(request.Id))
            {
                return BadRequest(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.PVCodeAlreadyExists)
                });
            }
            
            var pv = new PV
            {
                Id = request.Id,
                Name = request.Name,
                MatriculaId = request.MatriculaId
            };
            
            var created = await _pvRepository.CreateAsync(pv);
            
            var response = new PVResponse
            {
                Id = created.Id,
                Name = created.Name,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt,
                MatriculaId = created.MatriculaId
            };
            
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                new ApiResponse<PVResponse>
                {
                    Success = true,
                    Data = response,
                    Message = _messageService.Get(AppMessage.PVCreatedSuccessfully)
                });
        }
        
        [HttpPut("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> Update(int id, PVRequest request)
        {
            if (id != request.Id)
            {
                return BadRequest(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = "ID mismatch"
                });
            }
            
            var existing = await _pvRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.PVNotFound)
                });
            }
            
            existing.Name = request.Name;
            existing.MatriculaId = request.MatriculaId;
            var updated = await _pvRepository.UpdateAsync(existing);
            
            var response = new PVResponse
            {
                Id = updated.Id,
                Name = updated.Name,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt,
                MatriculaId = updated.MatriculaId
            };
            
            return Ok(new ApiResponse<PVResponse>
            {
                Success = true,
                Data = response,
                Message = _messageService.Get(AppMessage.PVUpdatedSuccessfully)
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            var deleted = await _pvRepository.DeleteAsync(id);
            
            if (!deleted)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.PVNotFound)
                });
            }
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.PVDeletedSuccessfully)
            });
        }
    }
}
